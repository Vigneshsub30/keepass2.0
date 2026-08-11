#nullable enable

using System;
using System.IO;
using System.Text;
using System.Xml;

using KeePassLib.Utility;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="XmlUtilEx.CreateSecureReaderSettings"/> security
	/// hardening: DTD prohibition, entity expansion budget, and XXE blocking.
	/// </summary>
	public sealed class XmlHardeningTests
	{
		// ── Helpers ───────────────────────────────────────────────────── //

		private static XmlReader CreateReader(string xml, XmlReaderSettings? settings = null)
		{
			settings ??= XmlUtilEx.CreateSecureReaderSettings();
			return XmlReader.Create(new StringReader(xml), settings);
		}

		private static Exception? TryReadAll(XmlReader xr)
		{
			try
			{
				while(xr.Read()) { }
				return null;
			}
			catch(Exception ex)
			{
				return ex;
			}
		}

		// ── DTD prohibition ───────────────────────────────────────────── //

		[Fact]
		public void SecureSettings_DtdDocument_IsRejected()
		{
			const string xml = "<?xml version='1.0'?><!DOCTYPE foo [<!ELEMENT foo ANY>]><foo/>";
			using XmlReader xr = CreateReader(xml);
			Exception? ex = TryReadAll(xr);
			Assert.NotNull(ex);
			Assert.IsType<XmlException>(ex);
		}

		[Fact]
		public void SecureSettings_XxeViaSystemEntity_IsRejected()
		{
			const string xml =
				"<?xml version='1.0'?>" +
				"<!DOCTYPE foo [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]>" +
				"<foo>&xxe;</foo>";
			using XmlReader xr = CreateReader(xml);
			Exception? ex = TryReadAll(xr);
			Assert.NotNull(ex);
		}

		[Fact]
		public void SecureSettings_NoDtd_CleanDocument_Parses()
		{
			const string xml = "<root><entry>hello</entry></root>";
			using XmlReader xr = CreateReader(xml);
			Exception? ex = TryReadAll(xr);
			Assert.Null(ex);
		}

		// ── Entity expansion budget ─────────────────────────────────── //

		[Fact]
		public void SecureSettings_EntityExpansionExceedsLimit_IsRejected()
		{
			// Build a simulated document larger than the MaxCharactersInDocument
			// ceiling.  DTD is prohibited so we cannot use entity declarations;
			// instead we verify the character-count ceiling fires.
			var settings = XmlUtilEx.CreateSecureReaderSettings(
				maxCharsInDocument: 100L,   // very small limit for the test
				maxCharsFromEntities: 10L);

			// Build a document larger than 100 chars.
			string xml = "<root>" + new string('x', 200) + "</root>";

			// The ceiling can fire either at Create or during Read — handle both.
			Exception? caughtEx = null;
			try
			{
				using XmlReader xr = XmlReader.Create(new StringReader(xml), settings);
				caughtEx = TryReadAll(xr);
			}
			catch(Exception ex)
			{
				caughtEx = ex;
			}
			Assert.NotNull(caughtEx);
		}

		[Fact]
		public void SecureSettings_DocumentWithinLimit_Parses()
		{
			var settings = XmlUtilEx.CreateSecureReaderSettings(
				maxCharsInDocument: 1_000L,
				maxCharsFromEntities: 500L);

			const string xml = "<root><item>ok</item></root>";
			using XmlReader xr = XmlReader.Create(new StringReader(xml), settings);
			Exception? ex = TryReadAll(xr);
			Assert.Null(ex);
		}

		// ── Default ceiling values ────────────────────────────────────── //

		[Fact]
		public void CreateSecureReaderSettings_DefaultCeilings_AreDocumented()
		{
			XmlReaderSettings s = XmlUtilEx.CreateSecureReaderSettings();
			Assert.Equal(500_000_000L, s.MaxCharactersInDocument);
			Assert.Equal(10_000_000L,  s.MaxCharactersFromEntities);
		}

		[Fact]
		public void CreateSecureReaderSettings_DtdProcessing_IsProhibit()
		{
			XmlReaderSettings s = XmlUtilEx.CreateSecureReaderSettings();
			Assert.Equal(DtdProcessing.Prohibit, s.DtdProcessing);
		}

		[Fact]
		public void CreateSecureReaderSettings_XxeViaLocalFile_IsRejected()
		{
			// A reader created with secure settings must not resolve external files.
			// Verify indirectly: a SYSTEM entity DTD declaration is prohibited,
			// so XXE cannot proceed even if XmlResolver were set.
			const string xml =
				"<?xml version='1.0'?>" +
				"<!DOCTYPE foo [<!ENTITY ext SYSTEM 'file:///tmp/secret'>]>" +
				"<foo>&ext;</foo>";
			using XmlReader xr = CreateReader(xml);
			Exception? ex = TryReadAll(xr);
			// DTD is prohibited, so we expect an exception before XXE can trigger.
			Assert.NotNull(ex);
		}

		[Fact]
		public void CreateSecureReaderSettings_CustomCeilings_Applied()
		{
			XmlReaderSettings s = XmlUtilEx.CreateSecureReaderSettings(
				maxCharsInDocument: 1_000L, maxCharsFromEntities: 500L);
			Assert.Equal(1_000L, s.MaxCharactersInDocument);
			Assert.Equal(  500L, s.MaxCharactersFromEntities);
		}

		// ── CreateXmlReaderSettings baseline ─────────────────────────── //

		[Fact]
		public void CreateXmlReaderSettings_DtdProcessing_IsProhibit()
		{
			// The baseline settings (used by CreateXmlReader/LoadXmlDocument)
			// were upgraded from DtdProcessing.Ignore to DtdProcessing.Prohibit.
			XmlReaderSettings s = XmlUtilEx.CreateXmlReaderSettings();
			Assert.Equal(DtdProcessing.Prohibit, s.DtdProcessing);
		}

		[Fact]
		public void CreateXmlReaderSettings_DtdProhibit_RejectsDocWithDtd()
		{
			// Verify that a document with an inline DTD is rejected, confirming
			// XmlResolver null / DtdProhibit prevents DTD-based attacks.
			const string xml = "<?xml version='1.0'?><!DOCTYPE root [<!ELEMENT root ANY>]><root/>";
			XmlReaderSettings s = XmlUtilEx.CreateXmlReaderSettings();
			using XmlReader xr = XmlReader.Create(new StringReader(xml), s);
			Assert.NotNull(TryReadAll(xr));
		}

		// ── Deep nesting ──────────────────────────────────────────────── //

		[Fact]
		public void SecureSettings_ValidDepth_Parses()
		{
			// 50 levels of nesting — well within reasonable limits.
			var sb = new StringBuilder();
			for(int i = 0; i < 50; i++) sb.Append("<n>");
			sb.Append("leaf");
			for(int i = 0; i < 50; i++) sb.Append("</n>");

			using XmlReader xr = CreateReader(sb.ToString());
			Exception? ex = TryReadAll(xr);
			Assert.Null(ex);
		}
	}
}
