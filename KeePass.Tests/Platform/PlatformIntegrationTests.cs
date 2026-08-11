using System;
using KeePass.Core.Platform;
using Xunit;

namespace KeePass.Tests.Platform
{
    /// <summary>
    /// Unit tests verifying that the IPlatformIntegration contract and the
    /// TestPlatformIntegration stub behave correctly.
    ///
    /// These tests ensure:
    ///   - TestPlatformIntegration can be constructed and injected as IPlatformIntegration.
    ///   - IsSupported=false (the default) causes PlatformNotSupportedException on
    ///     service calls, enforcing the capability-query contract.
    ///   - IsSupported=true enables service operations and they behave as expected.
    ///   - PlatformId is settable to simulate different target OSes.
    /// </summary>
    public class PlatformIntegrationTests
    {
        // ── 1. Construction and injection ─────────────────────────────────────

        [Fact]
        public void TestStub_CanBeAssignedToInterface()
        {
            IPlatformIntegration platform = new TestPlatformIntegration();
            Assert.NotNull(platform);
            Assert.NotNull(platform.Clipboard);
            Assert.NotNull(platform.CredentialStore);
            Assert.NotNull(platform.AutoType);
            Assert.NotNull(platform.ScreenProtection);
        }

        [Fact]
        public void TestStub_DefaultPlatformId_IsWindows()
        {
            var platform = new TestPlatformIntegration();
            Assert.Equal(PlatformId.Windows, platform.PlatformId);
        }

        [Fact]
        public void TestStub_PlatformId_CanBeConfigured()
        {
            var platform = new TestPlatformIntegration { PlatformId = PlatformId.MacOS };
            Assert.Equal(PlatformId.MacOS, platform.PlatformId);
        }

        // ── 2. Clipboard capability gate ──────────────────────────────────────

        [Fact]
        public void Clipboard_DefaultNotSupported_SetTextThrows()
        {
            var platform = new TestPlatformIntegration();
            Assert.False(platform.Clipboard.IsSupported);
            Assert.Throws<PlatformNotSupportedException>(
                () => platform.Clipboard.SetText("secret"));
        }

        [Fact]
        public void Clipboard_DefaultNotSupported_GetTextThrows()
        {
            var platform = new TestPlatformIntegration();
            Assert.Throws<PlatformNotSupportedException>(
                () => platform.Clipboard.GetText());
        }

        [Fact]
        public void Clipboard_Supported_SetAndGetTextRoundTrips()
        {
            var platform = new TestPlatformIntegration();
            ((TestPlatformIntegration.TestClipboardService)platform.Clipboard).IsSupported = true;

            platform.Clipboard.SetText("MyPassword!");
            Assert.Equal("MyPassword!", platform.Clipboard.GetText());
        }

        [Fact]
        public void Clipboard_Supported_ClearIfOwner_ClearsOwnedContent()
        {
            var platform = new TestPlatformIntegration();
            ((TestPlatformIntegration.TestClipboardService)platform.Clipboard).IsSupported = true;

            platform.Clipboard.SetText("owned-content");
            platform.Clipboard.ClearIfOwner();
            Assert.Null(platform.Clipboard.GetText());
        }

        // ── 3. CredentialStore capability gate ────────────────────────────────

        [Fact]
        public void CredentialStore_DefaultNotSupported_StoreThrows()
        {
            var platform = new TestPlatformIntegration();
            Assert.Throws<PlatformNotSupportedException>(
                () => platform.CredentialStore.Store("key", new byte[] { 1, 2 }));
        }

        [Fact]
        public void CredentialStore_Supported_StoreAndRetrieveRoundTrips()
        {
            var platform = new TestPlatformIntegration();
            ((TestPlatformIntegration.TestCredentialStore)platform.CredentialStore).IsSupported = true;

            byte[] secret = new byte[] { 0xAA, 0xBB, 0xCC };
            platform.CredentialStore.Store("test-key", secret);
            byte[] retrieved = platform.CredentialStore.Retrieve("test-key");
            Assert.Equal(secret, retrieved);
        }

        [Fact]
        public void CredentialStore_Supported_Delete_RemovesEntry()
        {
            var platform = new TestPlatformIntegration();
            ((TestPlatformIntegration.TestCredentialStore)platform.CredentialStore).IsSupported = true;

            platform.CredentialStore.Store("k", new byte[] { 1 });
            platform.CredentialStore.Delete("k");
            Assert.Null(platform.CredentialStore.Retrieve("k"));
        }

        [Fact]
        public void CredentialStore_Supported_RetrieveNonexistentKey_ReturnsNull()
        {
            var platform = new TestPlatformIntegration();
            ((TestPlatformIntegration.TestCredentialStore)platform.CredentialStore).IsSupported = true;

            Assert.Null(platform.CredentialStore.Retrieve("no-such-key"));
        }

        // ── 4. AutoType capability gate ───────────────────────────────────────

        [Fact]
        public void AutoType_DefaultNotSupported_PerformAutoTypeThrows()
        {
            var platform = new TestPlatformIntegration();
            Assert.False(platform.AutoType.IsSupported);
            Assert.Throws<PlatformNotSupportedException>(
                () => platform.AutoType.PerformAutoType(
                    new AutoTypeContext("{PASSWORD}{ENTER}", null)));
        }

        [Fact]
        public void AutoType_Supported_PerformAutoType_RecordsContext()
        {
            var platform = new TestPlatformIntegration();
            var svc = (TestPlatformIntegration.TestAutoTypeService)platform.AutoType;
            svc.IsSupported = true;

            var ctx = new AutoTypeContext("{USERNAME}{TAB}{PASSWORD}", "MyApp");
            platform.AutoType.PerformAutoType(ctx);

            Assert.Same(ctx, svc.LastContext);
        }

        [Fact]
        public void AutoType_NullContext_ThrowsArgumentNullException()
        {
            var platform = new TestPlatformIntegration();
            var svc = (TestPlatformIntegration.TestAutoTypeService)platform.AutoType;
            svc.IsSupported = true;

            Assert.Throws<ArgumentNullException>(() => platform.AutoType.PerformAutoType(null));
        }

        // ── 5. ScreenProtection capability gate ───────────────────────────────

        [Fact]
        public void ScreenProtection_DefaultNotSupported_EnableIsNoOp()
        {
            var platform = new TestPlatformIntegration();
            Assert.False(platform.ScreenProtection.IsSupported);
            // Enable is a no-op when unsupported (does not throw)
            platform.ScreenProtection.Enable();
            var svc = (TestPlatformIntegration.TestScreenProtectionService)platform.ScreenProtection;
            Assert.False(svc.IsEnabled);
        }

        [Fact]
        public void ScreenProtection_Supported_EnableSetsIsEnabled()
        {
            var platform = new TestPlatformIntegration();
            var svc = (TestPlatformIntegration.TestScreenProtectionService)platform.ScreenProtection;
            svc.IsSupported = true;

            platform.ScreenProtection.Enable();
            Assert.True(svc.IsEnabled);

            platform.ScreenProtection.Disable();
            Assert.False(svc.IsEnabled);
        }

        // ── 6. AutoTypeContext construction ───────────────────────────────────

        [Fact]
        public void AutoTypeContext_NullSequence_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AutoTypeContext(null, "title"));
        }

        [Fact]
        public void AutoTypeContext_EmptySequence_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AutoTypeContext(string.Empty, "title"));
        }

        [Fact]
        public void AutoTypeContext_ValidSequence_PropertiesSet()
        {
            var ctx = new AutoTypeContext("{PASSWORD}{ENTER}", "MyApp - Login");
            Assert.Equal("{PASSWORD}{ENTER}", ctx.Sequence);
            Assert.Equal("MyApp - Login", ctx.TargetWindowTitle);
        }
    }
}
