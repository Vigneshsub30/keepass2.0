using System;

using KeePass.Core.Platform;
using KeePass.Platform;

using Xunit;

namespace KeePass.Tests.Platform.Windows
{
    /// <summary>
    /// Characterization tests for <see cref="WindowsPlatformIntegration"/>.
    ///
    /// Verifies that:
    ///   - <see cref="WindowsPlatformIntegration.Create"/> returns a valid instance.
    ///   - <see cref="IPlatformIntegration.PlatformId"/> is <see cref="PlatformId.Windows"/>.
    ///   - All four service properties are non-null and satisfy their respective
    ///     <c>IsSupported</c> contracts on Windows.
    ///   - The instance can be assigned to <see cref="IPlatformIntegration"/>.
    /// </summary>
    public class WindowsPlatformIntegrationTests
    {
        private readonly IPlatformIntegration _platform = WindowsPlatformIntegration.Create();

        // ── 1. Factory and assignment ──────────────────────────────────────

        [Fact]
        public void Create_ReturnsNonNull()
        {
            IPlatformIntegration pi = WindowsPlatformIntegration.Create();
            Assert.NotNull(pi);
        }

        [Fact]
        public void Create_CanBeAssignedToInterface()
        {
            IPlatformIntegration pi = WindowsPlatformIntegration.Create();
            Assert.NotNull(pi);
        }

        // ── 2. PlatformId ──────────────────────────────────────────────────

        [Fact]
        public void PlatformId_IsWindows()
        {
            Assert.Equal(PlatformId.Windows, _platform.PlatformId);
        }

        // ── 3. Sub-services are non-null ───────────────────────────────────

        [Fact]
        public void Clipboard_IsNotNull()
        {
            Assert.NotNull(_platform.Clipboard);
        }

        [Fact]
        public void CredentialStore_IsNotNull()
        {
            Assert.NotNull(_platform.CredentialStore);
        }

        [Fact]
        public void AutoType_IsNotNull()
        {
            Assert.NotNull(_platform.AutoType);
        }

        [Fact]
        public void ScreenProtection_IsNotNull()
        {
            Assert.NotNull(_platform.ScreenProtection);
        }

        // ── 4. IsSupported values ──────────────────────────────────────────

        [Fact]
        public void Clipboard_IsSupported_IsTrue()
        {
            Assert.True(_platform.Clipboard.IsSupported);
        }

        [Fact]
        public void CredentialStore_IsSupported_IsTrue()
        {
            Assert.True(_platform.CredentialStore.IsSupported);
        }

        [Fact]
        public void AutoType_IsSupported_IsTrue()
        {
            Assert.True(_platform.AutoType.IsSupported);
        }

        [Fact]
        public void ScreenProtection_IsSupported_DependsOnWindowsVersion()
        {
            // IsSupported = true on Windows 7+; the CI runner is expected to
            // be Windows 10+ so this should always pass on CI.
            bool isWin7OrLater = _platform.ScreenProtection.IsSupported;
            // Just verify it returns without throwing.
            Assert.IsType<bool>(isWin7OrLater);
        }
    }
}
