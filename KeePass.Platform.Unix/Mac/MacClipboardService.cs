using System;
using System.Threading;

using KeePass.Core.Platform;
using KeePass.Platform.Unix.Shared;

namespace KeePass.Platform.Unix.Mac
{
    /// <summary>
    /// macOS implementation of <see cref="IClipboardService"/>.
    ///
    /// Delegates to the <c>pbcopy</c> (write) and <c>pbpaste</c> (read) CLI
    /// tools that ship with every macOS installation and always access the
    /// general pasteboard.  This mirrors the approach already used inside
    /// KeePass's own <c>ClipboardUtil.Unix.cs</c> for the Mono/macOS path.
    ///
    /// Clipboard ownership is tracked hash-based (SHA-256 of the last text
    /// set by this process) because the NSPasteboard change-count is not
    /// accessible without ObjC interop.
    /// </summary>
    public sealed class MacClipboardService : IClipboardService
    {
        private readonly ClipboardOwnerTracker _tracker = new ClipboardOwnerTracker();
        private Timer _autoClearTimer;
        private readonly object _timerLock = new object();

        /// <inheritdoc/>
        public bool IsSupported => true;

        /// <inheritdoc/>
        public void SetText(string text)
        {
            if (text == null) throw new ArgumentNullException("text");

            // pbcopy reads from stdin; no additional arguments needed for the
            // general pasteboard (it is the default).
            ProcessRunner.RunSilent("pbcopy", string.Empty, stdinData: text);
            _tracker.Record(text);
        }

        /// <inheritdoc/>
        public string GetText()
        {
            string result = ProcessRunner.Run("pbpaste", string.Empty);
            return result ?? string.Empty;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            CancelAutoClearTimer();
            ProcessRunner.RunSilent("pbcopy", string.Empty, stdinData: string.Empty);
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
