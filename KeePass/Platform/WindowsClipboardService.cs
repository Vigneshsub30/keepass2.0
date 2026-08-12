/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Threading;

using KeePass.Core.Platform;
using KeePass.Util;

namespace KeePass.Platform
{
    /// <summary>
    /// Windows implementation of <see cref="IClipboardService"/>.
    ///
    /// Delegates all operations to the existing <see cref="ClipboardUtil"/>
    /// static class, which handles Windows-native clipboard access via
    /// OpenClipboard/EmptyClipboard/SetClipboardData, clipboard viewer
    /// ignore formats, and hash-based ownership tracking.
    ///
    /// <see cref="SetWithAutoClear"/> schedules a one-shot
    /// <see cref="System.Threading.Timer"/> that calls <see cref="Clear"/>
    /// after the requested timeout — the same semantics as the existing
    /// clipboard clear-after-N-seconds feature in KeePass.
    /// </summary>
    public sealed class WindowsClipboardService : IClipboardService
    {
        private Timer _autoClearTimer;
        private readonly object _timerLock = new object();

        /// <inheritdoc/>
        /// <remarks>Always <c>true</c> on Windows.</remarks>
        public bool IsSupported => true;

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="text"/> is null.
        /// </exception>
        public void SetText(string text)
        {
            if (text == null) throw new ArgumentNullException("text");

            // bSprCompile=false: the IClipboardService contract deals with plain
            // text strings; SPR compilation is application-level concern.
            // bIsEntryInfo=false: no policy check or event raising at this layer.
            ClipboardUtil.Copy(text, false, false, null, null, IntPtr.Zero);
        }

        /// <inheritdoc/>
        public string GetText()
        {
            return ClipboardUtil.GetText();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            CancelAutoClearTimer();
            ClipboardUtil.Clear();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Clears the clipboard only if the data on it was placed there by
        /// this application (determined by SHA-256 hash of the last copied
        /// string, maintained by <see cref="ClipboardUtil"/>).
        /// </remarks>
        public void ClearIfOwner()
        {
            ClipboardUtil.ClearIfOwner();
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="text"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="timeout"/> is zero or negative.
        /// </exception>
        public void SetWithAutoClear(string text, TimeSpan timeout)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("timeout",
                    "Auto-clear timeout must be positive.");

            SetText(text);
            ScheduleAutoClear(timeout);
        }

        // ── Private helpers ────────────────────────────────────────────────

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
            lock (_timerLock)
            {
                CancelAutoClearTimerLocked();
            }
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
            lock (_timerLock)
            {
                CancelAutoClearTimerLocked();
            }
            // ClearIfOwner so we don't wipe content the user placed manually.
            ClipboardUtil.ClearIfOwner();
        }
    }
}
