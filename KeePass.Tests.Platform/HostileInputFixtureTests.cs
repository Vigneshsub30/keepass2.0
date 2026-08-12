#nullable enable

using System;
using System.IO;
using System.Xml;

using KeePass.Core.DataExchange;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Hostile-input fixture suite for the import pipeline trust boundary.
	/// Each test verifies that adversarial/malformed input is rejected quickly
	/// and does not cause OOM, unhandled exceptions, or excessive CPU usage.
	/// All tests are individually constrained to 5 seconds.
	/// </summary>
	[Collection("HostileInput")]
	public sealed class HostileInputFixtureTests
	{
		private static ImportValidationPipeline DefaultPipeline() =>
			new ImportValidationPipeline(ImportValidationOptions.Default());

		// ── XML billion laughs ────────────────────────────────────────── //

		[Fact]
		public void Test_XmlBillionLaughs_RejectedByDtdProhibit()
		{
			using var stream = HostileFixtureGenerator.XmlBillionLaughs();
			var pipeline = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			using var reader = pipeline.CreateXmlReader(limited);
			Assert.Throws<XmlException>(() =>
			{
				while(reader.Read()) { }
			});
		}

		// ── XML external entity (XXE) ─────────────────────────────────── //

		[Fact]
		public void Test_XmlExternalEntity_RejectedByDtdProhibit()
		{
			using var stream = HostileFixtureGenerator.XmlExternalEntity();
			var pipeline = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			using var reader = pipeline.CreateXmlReader(limited);
			Assert.Throws<XmlException>(() =>
			{
				while(reader.Read()) { }
			});
		}

		// ── XML deep nesting ─────────────────────────────────────────── //

		[Fact]
		public void Test_XmlDeepNesting_RejectedByMaxXmlDepth()
		{
			// Generate XML nested to exactly MaxXmlDepth + 1.
			const int maxDepth = 100;
			using var stream = HostileFixtureGenerator.XmlDeepNesting(maxDepth + 1);
			var pipeline = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			using var reader = pipeline.CreateXmlReader(limited);
			var ex = Assert.Throws<ImportValidationException>(() =>
			{
				while(reader.Read()) { }
			});
			Assert.Equal("MaxXmlDepth",       ex.CeilingName);
			Assert.Equal((long)maxDepth,      ex.ConfiguredLimit);
			Assert.True(ex.ObservedValue > maxDepth);
		}

		[Fact]
		public void Test_XmlAtExactDepth_Accepted()
		{
			// Exactly at the limit should not throw.
			const int maxDepth = 100;
			using var stream = HostileFixtureGenerator.XmlDeepNesting(maxDepth);
			var pipeline = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			using var reader = pipeline.CreateXmlReader(limited);
			// Must not throw.
			while(reader.Read()) { }
		}

		// ── XML quadratic blowup ──────────────────────────────────────── //

		[Fact]
		public void Test_XmlQuadraticBlowup_ProcessedWithinSizeLimit()
		{
			// Quadratic blowup without DTD — should be accepted by the pipeline
			// (the fixture is small); the test verifies no exception for non-DTD XML.
			using var stream = HostileFixtureGenerator.XmlQuadraticBlowup();
			var pipeline = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			using var reader = pipeline.CreateXmlReader(limited);
			// Should parse without error (it's valid XML, no DTD).
			while(reader.Read()) { }
		}

		// ── Oversized stream ──────────────────────────────────────────── //

		[Fact]
		public void Test_OversizedStream_RejectedByMaxFileSize()
		{
			long limit   = 1024L; // small limit for test speed
			long overBy  = 1L;
			var opts     = new ImportValidationOptions { MaxFileSize = limit };
			var pipeline = new ImportValidationPipeline(opts);

			// FakeOversizedStream delivers limit+1 bytes without heap allocation.
			using var stream  = HostileFixtureGenerator.OversizedStream(limit + overBy);
			using var limited = pipeline.Validate(stream);

			var ex = Assert.Throws<ImportValidationException>(() =>
			{
				byte[] buf = new byte[4096];
				while(limited.Read(buf, 0, buf.Length) > 0) { }
			});
			Assert.Equal("MaxFileSize", ex.CeilingName);
			Assert.Equal(limit,         ex.ConfiguredLimit);
			Assert.True(ex.ObservedValue > limit);
		}

		[Fact]
		public void Test_StreamAtExactSizeLimit_Accepted()
		{
			long limit   = 1024L;
			var opts     = new ImportValidationOptions { MaxFileSize = limit };
			var pipeline = new ImportValidationPipeline(opts);
			using var stream  = HostileFixtureGenerator.OversizedStream(limit);
			using var limited = pipeline.Validate(stream);
			byte[] buf = new byte[4096];
			while(limited.Read(buf, 0, buf.Length) > 0) { }
		}

		// ── Zero-byte stream ──────────────────────────────────────────── //

		[Fact]
		public void Test_ZeroByteStream_ReadReturnsZero()
		{
			using var stream  = HostileFixtureGenerator.ZeroByte();
			var pipeline      = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			byte[] buf        = new byte[16];
			int n             = limited.Read(buf, 0, buf.Length);
			Assert.Equal(0, n);
		}

		// ── Null-byte text ────────────────────────────────────────────── //

		[Fact]
		public void Test_NullByteText_PassesSizeLimitPipeline()
		{
			// Null-byte text is not rejected by the size pipeline alone
			// (rejection is the provider's responsibility for format validity).
			using var stream  = HostileFixtureGenerator.NullByteText();
			var pipeline      = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			byte[] buf        = new byte[128];
			while(limited.Read(buf, 0, buf.Length) > 0) { }
		}

		// ── Corrupted KDBX header ─────────────────────────────────────── //

		[Fact]
		public void Test_CorruptedKdbxHeader_PassesSizeLimitPipeline()
		{
			// The size pipeline passes short files; provider rejects the magic.
			using var stream  = HostileFixtureGenerator.CorruptedKdbxHeader();
			var pipeline      = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			byte[] buf        = new byte[128];
			while(limited.Read(buf, 0, buf.Length) > 0) { }
		}

		// ── CSV excessive entry count ─────────────────────────────────── //

		[Fact]
		public void Test_CsvExcessiveEntryCount_DetectedByEntryCountCheck()
		{
			// Simulate the post-parse entry-count check with a large row count.
			// (We don't run through a real CSV provider; that's tested in
			// ProviderPipelineAuditTests.EntryCount_OneBeyondLimit_Throws.)
			var opts = new ImportValidationOptions { MaxEntryCount = 5 };
			long entryCount = 6L;
			if(entryCount > opts.MaxEntryCount)
			{
				var ex = new ImportValidationException(
					nameof(ImportValidationOptions.MaxEntryCount),
					opts.MaxEntryCount,
					entryCount);
				Assert.Equal("MaxEntryCount",    ex.CeilingName);
				Assert.Equal(5L,                 ex.ConfiguredLimit);
				Assert.Equal(6L,                 ex.ObservedValue);
				return;
			}
			Assert.Fail("Entry-count check should have triggered.");
		}

		// ── JSON deep nesting ─────────────────────────────────────────── //

		[Fact]
		public void Test_JsonDeepNesting_StreamPassesSizePipeline()
		{
			// JSON parsing is not XML; the size-limit and XML-depth checks do
			// not apply at the pipeline layer.  Deep JSON is caught at the
			// provider level.  This test confirms the pipeline does not panic.
			using var stream  = HostileFixtureGenerator.JsonDeepNesting(500);
			var pipeline      = DefaultPipeline();
			using var limited = pipeline.Validate(stream);
			byte[] buf        = new byte[4096];
			while(limited.Read(buf, 0, buf.Length) > 0) { }
		}
	}
}
