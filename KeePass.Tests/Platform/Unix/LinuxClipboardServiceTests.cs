using System;
using KeePass.Core.Platform;
using KeePass.Platform.Unix.Linux;
using Xunit;

namespace KeePass.Tests.Platform.Unix
{
    /// <summary>
    /// Characterization tests for <see cref="LinuxClipboardService"/>.
    /// Verifies parameter-validation contracts without invoking any clipboard
    /// helper process.  Runs on all platforms.
    /// </summary>
    public class LinuxClipboardServiceTests
    {
        [Fact]
        public void LinuxClipboardService_IsSupported_DoesNotThrow()
        {
            var svc = new LinuxClipboardService();
            // Value depends on whether wl-copy/xclip/xsel is installed.
            // We only assert no exception is thrown.
            bool _ = svc.IsSupported;
        }

        [Fact]
        public void LinuxClipboardService_SetText_NullText_ThrowsArgumentNull()
        {
            var svc = new LinuxClipboardService();
            Assert.Throws<ArgumentNullException>(() => svc.SetText(null));
        }

        [Fact]
        public void LinuxClipboardService_SetWithAutoClear_NullText_ThrowsArgumentNull()
        {
            var svc = new LinuxClipboardService();
            Assert.Throws<ArgumentNullException>(
                () => svc.SetWithAutoClear(null, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void LinuxClipboardService_SetWithAutoClear_ZeroTimeout_ThrowsArgumentOutOfRange()
        {
            var svc = new LinuxClipboardService();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => svc.SetWithAutoClear("text", TimeSpan.Zero));
        }

        [Fact]
        public void LinuxClipboardService_SetWithAutoClear_NegativeTimeout_ThrowsArgumentOutOfRange()
        {
            var svc = new LinuxClipboardService();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => svc.SetWithAutoClear("text", TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void LinuxClipboardService_ImplementsIClipboardService()
        {
            IClipboardService svc = new LinuxClipboardService();
            Assert.NotNull(svc);
        }
    }
}
