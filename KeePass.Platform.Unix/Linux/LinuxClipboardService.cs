using System;
using System.Threading;

using KeePass.Core.Platform;
using KeePass.Platform.Unix.Shared;

namespace KeePass.Platform.Unix.Linux
{
    /// <summary>
    /// Linux implementation of <see cref="IClipboardService"/>.
    ///
    /// Supports both X11 and Wayland sessions by probing for the available
    /// clipboard helper at first use:
    /// <list type="bullet">
    ///   <item>Wayland: <c>wl-copy</c> / <c>wl-paste</c> (wl-clipboard package)</item>
    ///   <item>X11: <c>xclip -selection clipboard</c> (preferred) or
    ///     <c>xsel --clipboard</c></item>
    /// </list>
    ///
    /// <see cref="IsSupported"/> returns <c>true</c> only when at least one
    /// helper is found on <c>PATH</c>.  Callers should check this before
    /// invoking clipboard methods.
    ///
    /// Ownership tracking is hash-based (see <see cref="ClipboardOwnerTracker"/>).
    /// </summary>
    public sealed class LinuxClipboardService : IClipboardService
    {
        private enum ClipBackend { Unknown, Wayland, Xclip, Xsel }

        private ClipBackend _backend = ClipBackend.Unknown;
        private readonly object _backendLock = new object();

        private readonly ClipboardOwnerTracker _tracker = new ClipboardOwnerTracker();
        private Timer _autoClearTimer;
        private readonly object _timerLock = new object();

        /// <inheritdoc/>
        public bool IsSupported => DetectBackend() != ClipBackend.Unknown;

        /// <inheritdoc/>
        public void SetText(string text)
        {
            if (text == null) throw new ArgumentNullException("text");

            ClipBackend be = RequireBackend();
            bool ok;
            switch (be)
            {
                case ClipBackend.Wayland:
                    ok = ProcessRunner.RunSilent("wl-copy", string.Empty, stdinData: text);
                    break;
                case ClipBackend.Xclip:
                    ok = ProcessRunner.RunSilent("xclip", "-selection clipboard", stdinData: text);
                    break;
                default: // Xsel
                    ok = ProcessRunner.RunSilent("xsel", "--clipboard --input", stdinData: text);
                    break;
            }

            if (ok) _tracker.Record(text);
        }

        /// <inheritdoc/>
        public string GetText()
        {
            ClipBackend be = DetectBackend();
            if (be == ClipBackend.Unknown) return string.Empty;

            string result;
            switch (be)
            {
                case ClipBackend.Wayland:
                    result = ProcessRunner.Run("wl-paste", "--no-newline");
                    break;
                case ClipBackend.Xclip:
                    result = ProcessRunner.Run("xclip", "-selection clipboard -o");
                    break;
                default: // Xsel
                    result = ProcessRunner.Run("xsel", "--clipboard --output");
                    break;
            }
            return result ?? string.Empty;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            CancelAutoClearTimer();
            ClipBackend be = DetectBackend();
            if (be == ClipBackend.Unknown) return;

            switch (be)
            {
                case ClipBackend.Wayland:
                    ProcessRunner.RunSilent("wl-copy", "--clear");
                    break;
                case ClipBackend.Xclip:
                    ProcessRunner.RunSilent("xclip", "-selection clipboard",
                        stdinData: string.Empty);
                    break;
                default:
                    ProcessRunner.RunSilent("xsel", "--clipboard --clear");
                    break;
            }
            _tracker.Forget();
        }

        /// <inheritdoc/>
        public void ClearIfOwner()
        {
            string current = GetText();
            if (_tracker.IsOwner(current))
                Clear();
        }

        /// <inheritdoc/>
        public void SetWithAutoClear(string text, TimeSpan timeout)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("timeout",
                    "Auto-clear timeout must be positive.");

            SetText(text);
            ScheduleAutoClear(timeout);
        }

        // ── Backend detection ─────────────────────────────────────────────

        private ClipBackend DetectBackend()
        {
            lock (_backendLock)
            {
                if (_backend != ClipBackend.Unknown) return _backend;

                // Prefer Wayland when the session is Wayland-native.
                string waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                if (!string.IsNullOrEmpty(waylandDisplay) &&
                    ProcessRunner.Run("which", "wl-copy") != null)
                {
                    _backend = ClipBackend.Wayland;
                    return _backend;
                }

                if (ProcessRunner.Run("which", "xclip") != null)
                {
                    _backend = ClipBackend.Xclip;
                    return _backend;
                }

                if (ProcessRunner.Run("which", "xsel") != null)
                {
                    _backend = ClipBackend.Xsel;
                    return _backend;
                }

                // Leave as Unknown — IsSupported returns false.
                return ClipBackend.Unknown;
            }
        }

        private ClipBackend RequireBackend()
        {
            ClipBackend be = DetectBackend();
            if (be == ClipBackend.Unknown)
                throw new PlatformNotSupportedException(
                    "No clipboard helper found. " +
                    "Install wl-clipboard (Wayland), xclip, or xsel.");
            return be;
        }

        // ── Timer helpers ─────────────────────────────────────────────────

        private void ScheduleAutoClear(TimeSpan delay)
        {
            lock (_timerLock)
            {
                CancelAutoClearTimerLocked();
                _autoClearTimer = new Timer(OnAutoClearElapsed, null,
                    (long)delay.TotalMilliseconds, Timeout.Infinite);
            }
        }

        private void CancelAutoClearTimer()
        {
            lock (_timerLock) { CancelAutoClearTimerLocked(); }
        }

        private void CancelAutoClearTimerLocked()
        {
            if (_autoClearTimer != null)
            {
                _autoClearTimer.Dispose();
                _autoClearTimer = null;
            }
        }

        private void OnAutoClearElapsed(object state)
        {
            lock (_timerLock) { CancelAutoClearTimerLocked(); }
            ClearIfOwner();
        }
    }
}
