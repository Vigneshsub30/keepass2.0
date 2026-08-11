using System;

using KeePass.Core.Platform;
using KeePass.Platform;

using Xunit;

namespace KeePass.Tests.Platform.Windows
{
    /// <summary>
    /// Characterization tests for <see cref="WindowsClipboardService"/>.
    ///
    /// Tests in this file verify the contract behavior of the service:
    ///   - <see cref="IClipboardService.IsSupported"/> is always <c>true</c> on Windows.
    ///   - Parameter validation: null/invalid inputs throw the expected exceptions.
    ///   - <see cref="IClipboardService.SetWithAutoClear"/> validates its timeout.
    ///   - The service can be assigned to <see cref="IClipboardService"/> (polymorphism).
    ///
    /// Integration tests (clipboard set/get/clear cycle, auto-clear timer expiry)
    /// require a live Windows desktop session and are tagged as such.
    /// </summary>
    public class WindowsClipboardServiceTests
    {
        private readonly IClipboardService _svc = new WindowsClipboardService();

        // ── 1. Contract: IsSupported ───────────────────────────────────────

        [Fact]
        public void IsSupported_IsTrue_OnWindows()
        {
            Assert.True(_svc.IsSupported);
        }

        [Fact]
        public void CanBeAssignedToInterface()
        {
            IClipboardService asInterface = new WindowsClipboardService();
            Assert.NotNull(asInterface);
        }

        // ── 2. Parameter validation: SetText ──────────────────────────────

        [Fact]
        public void SetText_NullText_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.SetText(null));
        }

        // ── 3. Parameter validation: SetWithAutoClear ─────────────────────

        [Fact]
        public void SetWithAutoClear_NullText_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _svc.SetWithAutoClear(null, TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public void SetWithAutoClear_ZeroTimeout_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _svc.SetWithAutoClear("test", TimeSpan.Zero));
        }

        [Fact]
        public void SetWithAutoClear_NegativeTimeout_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _svc.SetWithAutoClear("test", TimeSpan.FromSeconds(-1)));
        }
    }
}
