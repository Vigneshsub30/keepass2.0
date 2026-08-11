using System;
using KeePass.Core.Platform;
using KeePass.Platform.Unix.Mac;
using Xunit;

namespace KeePass.Tests.Platform.Unix
{
    /// <summary>
    /// Characterization tests for <see cref="MacClipboardService"/>.
    ///
    /// These tests verify the contract surface (parameter validation,
    /// IsSupported flag, interface assignment) without invoking actual
    /// pbcopy/pbpaste processes.  They run on all platforms.
    /// </summary>
    public class MacClipboardServiceTests
    {
        [Fact]
        public void MacClipboardService_IsSupported_ReturnsTrue()
        {
            // macOS is always declared supported regardless of host OS;
            // the platform guard is applied at the integration factory level.
            var svc = new MacClipboardService();
            Assert.True(svc.IsSupported);
        }

        [Fact]
        public void MacClipboardService_SetText_NullText_ThrowsArgumentNull()
        {
            var svc = new MacClipboardService();
            Assert.Throws<ArgumentNullException>(() => svc.SetText(null));
        }

        [Fact]
        public void MacClipboardService_SetWithAutoClear_NullText_ThrowsArgumentNull()
        {
            var svc = new MacClipboardService();
            Assert.Throws<ArgumentNullException>(
                () => svc.SetWithAutoClear(null, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void MacClipboardService_SetWithAutoClear_ZeroTimeout_ThrowsArgumentOutOfRange()
        {
            var svc = new MacClipboardService();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => svc.SetWithAutoClear("text", TimeSpan.Zero));
        }

        [Fact]
        public void MacClipboardService_SetWithAutoClear_NegativeTimeout_ThrowsArgumentOutOfRange()
        {
            var svc = new MacClipboardService();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => svc.SetWithAutoClear("text", TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void MacClipboardService_ImplementsIClipboardService()
        {
            IClipboardService svc = new MacClipboardService();
            Assert.NotNull(svc);
        }
    }
}
