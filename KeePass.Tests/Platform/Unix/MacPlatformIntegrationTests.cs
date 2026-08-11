using KeePass.Core.Platform;
using KeePass.Platform.Unix.Mac;
using Xunit;

namespace KeePass.Tests.Platform.Unix
{
    /// <summary>
    /// Characterization tests for <see cref="MacPlatformIntegration"/>.
    /// Verifies that <see cref="MacPlatformIntegration.Create"/> wires all
    /// sub-services correctly and reports the expected capability flags.
    /// </summary>
    public class MacPlatformIntegrationTests
    {
        [Fact]
        public void MacPlatformIntegration_Create_ReturnsnNonNull()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.NotNull(pi);
        }

        [Fact]
        public void MacPlatformIntegration_PlatformId_IsMacOS()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.Equal(PlatformId.MacOS, pi.PlatformId);
        }

        [Fact]
        public void MacPlatformIntegration_Clipboard_IsNotNull()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.NotNull(pi.Clipboard);
        }

        [Fact]
        public void MacPlatformIntegration_Clipboard_IsSupported()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.True(pi.Clipboard.IsSupported);
        }

        [Fact]
        public void MacPlatformIntegration_CredentialStore_IsNotNull()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.NotNull(pi.CredentialStore);
        }

        [Fact]
        public void MacPlatformIntegration_CredentialStore_IsSupported()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.True(pi.CredentialStore.IsSupported);
        }

        [Fact]
        public void MacPlatformIntegration_AutoType_IsNotNull()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.NotNull(pi.AutoType);
        }

        [Fact]
        public void MacPlatformIntegration_AutoType_IsNotSupported()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.False(pi.AutoType.IsSupported);
        }

        [Fact]
        public void MacPlatformIntegration_ScreenProtection_IsNotNull()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.NotNull(pi.ScreenProtection);
        }

        [Fact]
        public void MacPlatformIntegration_ScreenProtection_IsNotSupported()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.False(pi.ScreenProtection.IsSupported);
        }

        [Fact]
        public void MacPlatformIntegration_ImplementsIPlatformIntegration()
        {
            IPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.NotNull(pi);
        }
    }
}
