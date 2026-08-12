#nullable enable

using System;
using System.IO;
using System.Text;
using System.Xml;

using KeePass.Core.DataExchange;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="ImportValidationPipeline"/>,
	/// <see cref="SizeLimitingStream"/>, and <see cref="DepthLimitingXmlReader"/>.
	/// </summary>
	public sealed class ImportValidationPipelineTests
	{
		// ── ImportValidationOptions ────────────────────────────────────── //

		[Fact]
		public void Options_Defaults_AreReasonable()
		{
			var opts = ImportValidationOptions.Default();
			Assert.Equal(524_288_000L, opts.MaxFileSize); // 500 MB
			Assert.Equal(1_000_000,    opts.MaxEntryCount);
			Assert.Equal(100,          opts.MaxXmlDepth);
			Assert.True(opts.ProhibitDtd);
		}

		// ── ImportValidationException ──────────────────────────────────── //

		[Fact]
		public void Exception_Message_ContainsCeilingNameAndLimits()
		{
			var ex = new ImportValidationException("MaxFileSize", 500L, 501L);
			Assert.Contains("MaxFileSize", ex.Message);
			Assert.Contains("500",         ex.Message);
			Assert.Contains("501",         ex.Message);
		}

		[Fact]
		public void Exception_NegativeObserved_OmitsObservedFromMessage()
		{
			var ex = new ImportValidationException("MaxXmlDepth", 100L, -1L);
			Assert.Contains("MaxXmlDepth", ex.Message);
			Assert.Contains("100",         ex.Message);
		}

		// ── SizeLimitingStream ─────────────────────────────────────────── //

		[Fact]
		public void SizeLimiting_ExactLimit_DoesNotThrow()
		{
			byte[] data = new byte[10];
			using var inner = new MemoryStream(data);
			using var limited = new SizeLimitingStream(inner, 10);
			byte[] buf = new byte[10];
			int n = limited.Read(buf, 0, 10);
			Assert.Equal(10, n);
		}

		[Fact]
		public void SizeLimiting_OneBeyondLimit_Throws()
		{
			byte[] data = new byte[11];
			using var inner = new MemoryStream(data);
			using var limited = new SizeLimitingStream(inner, 10);
			byte[] buf = new byte[11];
			var ex = Assert.Throws<ImportValidationException>(
				() => limited.Read(buf, 0, 11));
			Assert.Equal("MaxFileSize", ex.CeilingName);
			Assert.Equal(10L,           ex.ConfiguredLimit);
			Assert.True(ex.ObservedValue > 10L);
		}

		[Fact]
		public void SizeLimiting_EmptyStream_ReturnsZero()
		{
			using var inner = new MemoryStream(Array.Empty<byte>());
			using var limited = new SizeLimitingStream(inner, 10);
			int n = limited.Read(new byte[5], 0, 5);
			Assert.Equal(0, n);
		}

		[Fact]
		public void SizeLimiting_MultipleReads_AccumulatesBytes()
		{
			byte[] data = new byte[12];
			using var inner = new MemoryStream(data);
			using var limited = new SizeLimitingStream(inner, 10);
			byte[] buf = new byte[6];
			limited.Read(buf, 0, 6); // 6 so far — ok
			var ex = Assert.Throws<ImportValidationException>(
				() => limited.Read(buf, 0, 6)); // 12 total — over 10
			Assert.Equal("MaxFileSize", ex.CeilingName);
		}

		[Fact]
		public void SizeLimiting_CannotSeek_Throws()
		{
			using var inner = new MemoryStream(new byte[10]);
			using var limited = new SizeLimitingStream(inner, 100);
			Assert.False(limited.CanSeek);
			Assert.Throws<NotSupportedException>(() => limited.Seek(0, SeekOrigin.Begin));
		}

		[Fact]
		public void SizeLimiting_NullInner_ThrowsArgNull()
		{
			Assert.Throws<ArgumentNullException>(() => new SizeLimitingStream(null!, 100));
		}

		// ── DepthLimitingXmlReader ─────────────────────────────────────── //

		private static DepthLimitingXmlReader BuildXmlReader(string xml, int maxDepth)
		{
			var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
			var inner = XmlReader.Create(new StringReader(xml), settings);
			return new DepthLimitingXmlReader(inner, maxDepth);
		}

		[Fact]
		public void DepthLimiting_FlatXml_DoesNotThrow()
		{
			string xml = "<root><a/><b/></root>";
			using var reader = BuildXmlReader(xml, 100);
			while(reader.Read()) { }
		}

		[Fact]
		public void DepthLimiting_AtExactDepth_DoesNotThrow()
		{
			// Build XML nested to exactly `maxDepth` levels.
			const int maxDepth = 5;
			var sb = new StringBuilder();
			for(int i = 0; i < maxDepth; i++) sb.Append("<x>");
			for(int i = 0; i < maxDepth; i++) sb.Append("</x>");
			using var reader = BuildXmlReader(sb.ToString(), maxDepth);
			while(reader.Read()) { }
		}

		[Fact]
		public void DepthLimiting_ExceedsMaxDepth_Throws()
		{
			const int maxDepth = 3;
			var sb = new StringBuilder();
			for(int i = 0; i <= maxDepth; i++) sb.Append("<x>"); // maxDepth + 1 levels
			for(int i = 0; i <= maxDepth; i++) sb.Append("</x>");
			using var reader = BuildXmlReader(sb.ToString(), maxDepth);
			var ex = Assert.Throws<ImportValidationException>(() =>
			{
				while(reader.Read()) { }
			});
			Assert.Equal("MaxXmlDepth", ex.CeilingName);
			Assert.Equal((long)maxDepth, ex.ConfiguredLimit);
		}

		// ── ImportValidationPipeline ───────────────────────────────────── //

		[Fact]
		public void Pipeline_Validate_ReturnsSizeLimitingStream()
		{
			var pipeline = new ImportValidationPipeline(ImportValidationOptions.Default());
			using var raw = new MemoryStream(new byte[100]);
			using var wrapped = pipeline.Validate(raw);
			Assert.IsType<SizeLimitingStream>(wrapped);
		}

		[Fact]
		public void Pipeline_CreateXmlReaderSettings_ProhibitsDtdByDefault()
		{
			var pipeline = new ImportValidationPipeline(ImportValidationOptions.Default());
			XmlReaderSettings settings = pipeline.CreateXmlReaderSettings();
			Assert.Equal(DtdProcessing.Prohibit, settings.DtdProcessing);
		}

		[Fact]
		public void Pipeline_CreateXmlReaderSettings_DtdAllowed_WhenProhibitDtdFalse()
		{
			var opts = new ImportValidationOptions { ProhibitDtd = false };
			var pipeline = new ImportValidationPipeline(opts);
			XmlReaderSettings settings = pipeline.CreateXmlReaderSettings();
			Assert.Equal(DtdProcessing.Ignore, settings.DtdProcessing);
		}

		[Fact]
		public void Pipeline_CreateXmlReader_RejectsXmlWithDtd()
		{
			string xml = "<?xml version=\"1.0\"?><!DOCTYPE foo [<!ENTITY e \"e\">]><root/>";
			var pipeline = new ImportValidationPipeline(ImportValidationOptions.Default());
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
			using var reader = pipeline.CreateXmlReader(stream);
			// XmlReader.Create with DtdProcessing.Prohibit will throw on Read()
			// when it encounters the DTD declaration.
			Assert.Throws<XmlException>(() =>
			{
				while(reader.Read()) { }
			});
		}

		[Fact]
		public void Pipeline_CreateXmlReader_RejectsDeepNesting()
		{
			var opts = new ImportValidationOptions { MaxXmlDepth = 3, ProhibitDtd = true };
			var pipeline = new ImportValidationPipeline(opts);
			var sb = new StringBuilder();
			for(int i = 0; i <= 3; i++) sb.Append("<x>"); // 4 levels — over 3
			for(int i = 0; i <= 3; i++) sb.Append("</x>");
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
			using var reader = pipeline.CreateXmlReader(stream);
			Assert.Throws<ImportValidationException>(() =>
			{
				while(reader.Read()) { }
			});
		}

		[Fact]
		public void Pipeline_Validate_NullStream_Throws()
		{
			var pipeline = new ImportValidationPipeline(ImportValidationOptions.Default());
			Assert.Throws<ArgumentNullException>(() => pipeline.Validate(null!));
		}

		[Fact]
		public void Pipeline_NullOptions_Throws()
		{
			Assert.Throws<ArgumentNullException>(
				() => new ImportValidationPipeline(null!));
		}

		/// <summary>
		/// Integration scenario: pipeline rejects a 1-byte-over-limit stream
		/// before a stub provider can process data.
		/// </summary>
		[Fact]
		public void Integration_OversizedStream_ProviderNeverInvoked()
		{
			bool providerInvoked = false;
			var opts = new ImportValidationOptions { MaxFileSize = 10L };
			var pipeline = new ImportValidationPipeline(opts);

			byte[] data = new byte[11]; // 1 byte over limit
			using var raw = new MemoryStream(data);
			using var limited = pipeline.Validate(raw);

			Assert.Throws<ImportValidationException>(() =>
			{
				byte[] buf = new byte[11];
				limited.Read(buf, 0, 11); // triggers the ceiling check
				providerInvoked = true;    // should never reach here
			});

			Assert.False(providerInvoked,
				"Provider must not be invoked when validation fails.");
		}
	}
}
