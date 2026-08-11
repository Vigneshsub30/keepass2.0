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

using KeePass.Core.Platform;
using KeePass.Util;
using KeePass.Util.Spr;

using KeePassLib;

namespace KeePass.Platform
{
    /// <summary>
    /// Windows implementation of <see cref="IAutoTypeService"/>.
    ///
    /// Delegates to the existing <see cref="AutoType"/> static class, which
    /// handles global hotkey registration, sequence compilation via
    /// <see cref="SprEngine"/>, obfuscated auto-type, and SendInput injection
    /// via <c>SendInputEx</c>.
    ///
    /// The <see cref="AutoTypeContext.Sequence"/> is passed directly as the
    /// pre-compiled sequence string (bypassing entry-level sequence selection)
    /// to ensure the caller retains full control over what keys are sent.
    ///
    /// Auto-type is Windows-only in v1; on other platforms <see cref="IsSupported"/>
    /// returns <c>false</c> and <see cref="PerformAutoType"/> throws
    /// <see cref="PlatformNotSupportedException"/>.
    /// </summary>
    public sealed class WindowsAutoTypeService : IAutoTypeService
    {
        /// <inheritdoc/>
        /// <remarks>Always <c>true</c> on Windows.</remarks>
        public bool IsSupported => true;

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="ctx"/> is null.
        /// </exception>
        public void PerformAutoType(AutoTypeContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException("ctx");

            // Create a minimal entry that satisfies the AutoType engine.
            // AutoTypeEnabled is true by default on new entries; we pass the
            // sequence explicitly via the strSeq overload so no entry-level
            // sequence resolution occurs.
            PwEntry pe = new PwEntry(false, false);

            // PerformIntoCurrentWindow checks AppPolicy.AutoTypeWithoutContext;
            // callers must ensure the appropriate policy is satisfied before
            // invoking this service.
            bool ok = AutoType.PerformIntoCurrentWindow(pe, null, ctx.Sequence);

            if (!ok)
                Debug.Assert(false, "WindowsAutoTypeService: auto-type failed for sequence: " +
                    ctx.Sequence);
        }
    }
}
