using KeePass.Core.Platform;
using KeePass.Platform.Unix.Linux;
using Xunit;

namespace KeePass.Tests.Platform.Unix
{
    /// <summary>
    /// Characterization tests for <see cref="LinuxPlatformIntegration"/>.
    /// Verifies that <see cref="LinuxPlatformIntegration.Create"/> wires all
    /// sub-services correctly and reports the expected capability flags.
    /// </summary>
    public class LinuxPlatformIntegrationTests
    {
        [Fact]
        public void LinuxPlatformIntegration_Create_ReturnsNonNull()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.NotNull(pi);
        }

        [Fact]
        public void LinuxPlatformIntegration_PlatformId_IsLinux()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.Equal(PlatformId.Linux, pi.PlatformId);
        }

        [Fact]
        public void LinuxPlatformIntegration_Clipboard_IsNotNull()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.NotNull(pi.Clipboard);
        }

        [Fact]
        public void LinuxPlatformIntegration_CredentialStore_IsNotNull()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.NotNull(pi.CredentialStore);
        }

        [Fact]
        public void LinuxPlatformIntegration_AutoType_IsNotNull()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.NotNull(pi.AutoType);
        }

        [Fact]
        public void LinuxPlatformIntegration_AutoType_IsNotSupported()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.False(pi.AutoType.IsSupported);
        }

        [Fact]
        public void LinuxPlatformIntegration_ScreenProtection_IsNotNull()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.NotNull(pi.ScreenProtection);
        }

        [Fact]
        public void LinuxPlatformIntegration_ScreenProtection_IsNotSupported()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.False(pi.ScreenProtection.IsSupported);
        }

        [Fact]
        public void LinuxPlatformIntegration_ImplementsIPlatformIntegration()
        {
            IPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.NotNull(pi);
        }
    }
}
