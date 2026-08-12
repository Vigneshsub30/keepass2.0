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
using System.Diagnostics;
using System.Windows.Forms;

using KeePass.Core.Platform;
using KeePass.Native;
using KeePass.Util;

namespace KeePass.Platform
{
    /// <summary>
    /// Windows implementation of <see cref="IScreenProtectionService"/>.
    ///
    /// Delegates to <c>NativeMethods.SetWindowDisplayAffinity</c>
    /// (<c>WDA_MONITOR</c> to enable, <c>WDA_NONE</c> to disable) which
    /// prevents the window from appearing in screenshots, screen recordings,
    /// and remote-desktop mirrors.  Available from Windows 7 onwards.
    ///
    /// The service applies the affinity to the main application window handle.
    /// If the window handle is not yet available the call is silently skipped.
    /// </summary>
    public sealed class WindowsScreenProtectionService : IScreenProtectionService
    {
        private bool _enabled;

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <c>true</c> when running on Windows 7 or later;
        /// <c>false</c> on earlier Windows versions where
        /// <c>SetWindowDisplayAffinity</c> is not available.
        /// </remarks>
        public bool IsSupported => WinUtil.IsAtLeastWindows7;

        /// <inheritdoc/>
        public void Enable()
        {
            if (!IsSupported || _enabled) return;
            SetAffinity(NativeMethods.WDA_MONITOR);
            _enabled = true;
        }

        /// <inheritdoc/>
        public void Disable()
        {
            if (!IsSupported || !_enabled) return;
            SetAffinity(NativeMethods.WDA_NONE);
            _enabled = false;
        }

        // ── Private helpers ────────────────────────────────────────────────

        private static void SetAffinity(uint affinity)
        {
            try
            {
                IntPtr hWnd = GetMainWindowHandle();
                if (hWnd == IntPtr.Zero) { Debug.Assert(false); return; }

                NativeMethods.SetWindowDisplayAffinity(hWnd, affinity);
            }
            catch (Exception) { Debug.Assert(false); }
        }

        private static IntPtr GetMainWindowHandle()
        {
            Form mf = Program.MainForm;
            if (mf != null && mf.IsHandleCreated)
                return mf.Handle;
            return Program.GetSafeMainWindowHandle();
        }
    }
}
