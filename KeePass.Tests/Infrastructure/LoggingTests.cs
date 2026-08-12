using System;
using System.Linq;

using KeePass.Core.Infrastructure;
using KeePassLib.Utility;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using Xunit;

namespace KeePass.Tests.Infrastructure
{
    /// <summary>
    /// Unit tests for the structured logging infrastructure introduced in WO-031.
    ///
    /// Tests use <see cref="FakeLogger"/> from
    /// <c>Microsoft.Extensions.Diagnostics.Testing</c> to capture log records
    /// without any I/O.
    /// </summary>
    public class LoggingTests
    {
        // ── KeePassLibLog ──────────────────────────────────────────────────

        [Fact]
        public void KeePassLibLog_DefaultConfiguration_DoesNotThrow()
        {
            // Default is NullLoggerFactory — all calls are no-ops.
            ILogger<LoggingTests> log = KeePassLibLog.Logger<LoggingTests>();
            // Should not throw; no output captured.
            log.LogInformation("Test from {Class}", nameof(LoggingTests));
        }

        [Fact]
        public void KeePassLibLog_ConfigureWithRealFactory_DoesNotThrow()
        {
            using ILoggerFactory realFactory = LoggerFactory.Create(b => b.AddFakeLogging());
            KeePassLibLog.Configure(realFactory);
            try
            {
                ILogger<LoggingTests> log = KeePassLibLog.Logger<LoggingTests>();
                // Should log without throwing.
                log.LogError("Something went wrong in {Component}", "TestComponent");
            }
            finally
            {
                KeePassLibLog.Configure(null);
            }
        }

        [Fact]
        public void KeePassLibLog_Configure_NullResetToNullLogger()
        {
            KeePassLibLog.Configure(null);
            // After reset, Logger<T> must still return a non-null logger.
            ILogger<LoggingTests> log = KeePassLibLog.Logger<LoggingTests>();
            Assert.NotNull(log);
        }

        [Fact]
        public void KeePassLibLog_Logger_WithCategoryName_ReturnsNonNull()
        {
            ILogger log = KeePassLibLog.Logger("KeePassLib.TestCategory");
            Assert.NotNull(log);
        }

        // ── FakeLogger capture ─────────────────────────────────────────────

        [Fact]
        public void FakeLogger_CapturesLogError_WithException()
        {
            FakeLogger<LoggingTests> fakeLogger = new FakeLogger<LoggingTests>();
            var ex = new InvalidOperationException("disk full");

            fakeLogger.LogError(ex, "IO operation failed for {Path}", "/tmp/test.kdbx");

            Assert.Equal(1, fakeLogger.Collector.Count);
            FakeLogRecord record = fakeLogger.Collector.GetSnapshot()[0];
            Assert.Equal(LogLevel.Error, record.Level);
            Assert.NotNull(record.Exception);
            Assert.Equal("disk full", record.Exception.Message);
        }

        [Fact]
        public void FakeLogger_CapturesLogWarning_WithStructuredParameters()
        {
            FakeLogger<LoggingTests> fakeLogger = new FakeLogger<LoggingTests>();

            fakeLogger.LogWarning("Proxy assignment failed for {Url}", "https://example.com");

            Assert.Equal(1, fakeLogger.Collector.Count);
            FakeLogRecord record = fakeLogger.Collector.GetSnapshot()[0];
            Assert.Equal(LogLevel.Warning, record.Level);
        }

        // ── LoggingConstants ───────────────────────────────────────────────

        [Fact]
        public void LoggingConstants_AllEventIds_HavePositiveId()
        {
            Assert.True(LoggingConstants.IoError.Id > 0);
            Assert.True(LoggingConstants.FileTransactionError.Id > 0);
            Assert.True(LoggingConstants.NetworkError.Id > 0);
            Assert.True(LoggingConstants.HttpRequestError.Id > 0);
            Assert.True(LoggingConstants.CryptoError.Id > 0);
            Assert.True(LoggingConstants.KeyDerivationError.Id > 0);
            Assert.True(LoggingConstants.KdbxReadWarning.Id > 0);
            Assert.True(LoggingConstants.KdbxReadError.Id > 0);
            Assert.True(LoggingConstants.KdbxWriteError.Id > 0);
            Assert.True(LoggingConstants.PluginError.Id > 0);
            Assert.True(LoggingConstants.ImportExportError.Id > 0);
            Assert.True(LoggingConstants.PlatformError.Id > 0);
        }

        [Fact]
        public void LoggingConstants_EventIds_AreUnique()
        {
            int[] ids = new[]
            {
                LoggingConstants.IoError.Id,
                LoggingConstants.FileTransactionError.Id,
                LoggingConstants.NetworkError.Id,
                LoggingConstants.HttpRequestError.Id,
                LoggingConstants.CryptoError.Id,
                LoggingConstants.KeyDerivationError.Id,
                LoggingConstants.KdbxReadWarning.Id,
                LoggingConstants.KdbxReadError.Id,
                LoggingConstants.KdbxWriteError.Id,
                LoggingConstants.PluginError.Id,
                LoggingConstants.ImportExportError.Id,
                LoggingConstants.PlatformError.Id,
            };

            Assert.Equal(ids.Length, ids.Distinct().Count());
        }

        // ── VaultContentRedactionPolicy ────────────────────────────────────

        [Fact]
        public void VaultContentRedactionPolicy_String_IsRedacted()
        {
            object result = VaultContentRedactionPolicy.Redact("MyPassword123");
            Assert.Equal("[REDACTED]", result as string);
        }

        [Fact]
        public void VaultContentRedactionPolicy_Null_ReturnsNull()
        {
            object result = VaultContentRedactionPolicy.Redact(null);
            Assert.Null(result);
        }

        [Fact]
        public void VaultContentRedactionPolicy_Int_IsPassedThrough()
        {
            object result = VaultContentRedactionPolicy.Redact(42);
            Assert.Equal(42, result);
        }

        [Fact]
        public void VaultContentRedactionPolicy_Bool_IsPassedThrough()
        {
            object result = VaultContentRedactionPolicy.Redact(true);
            Assert.Equal(true, result);
        }

        [Fact]
        public void VaultContentRedactionPolicy_Object_IsRedacted()
        {
            object result = VaultContentRedactionPolicy.Redact(new object());
            Assert.Equal("[REDACTED]", result as string);
        }

        [Fact]
        public void VaultContentRedactionPolicy_SafeException_ReturnsTypeAndMessage()
        {
            var ex = new InvalidOperationException("disk full");
            string result = VaultContentRedactionPolicy.SafeException(ex);
            Assert.Contains("InvalidOperationException", result);
            Assert.Contains("disk full", result);
        }

        [Fact]
        public void VaultContentRedactionPolicy_SafeException_NullReturnsEmpty()
        {
            string result = VaultContentRedactionPolicy.SafeException(null);
            Assert.Equal(string.Empty, result);
        }
    }
}
