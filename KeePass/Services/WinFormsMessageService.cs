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

using System.Windows.Forms;

using KeePass.Core.Services;
using KeePassLib;
using KeePassLib.Utility;

namespace KeePass.Services
{
    /// <summary>
    /// WinForms implementation of <see cref="IMessageService"/>.
    ///
    /// Delegates to the existing <see cref="KeePassLib.Utility.MessageService"/>
    /// static class so all existing logic (RTL, cross-thread marshalling, event
    /// firing for <c>MessageShowing</c> subscribers) is preserved.
    /// </summary>
    public sealed class WinFormsMessageService : IMessageService
    {
        /// <inheritdoc/>
        public void ShowInfo(string message, string title = null)
        {
            MessageService.ShowInfoEx(
                title ?? PwDefs.ShortProductName,
                message ?? string.Empty);
        }

        /// <inheritdoc/>
        public void ShowWarning(string message, string title = null)
        {
            // ShowWarning does not accept a custom title; the static class uses
            // PwDefs.ShortProductName internally.  For consistency, if a title
            // other than the default is requested, fall back to ShowInfoEx with
            // the warning icon.  For the default title path, delegate directly
            // to keep MessageShowing event and RTL logic intact.
            if (title == null || title == PwDefs.ShortProductName)
            {
                MessageService.ShowWarning(message ?? string.Empty);
            }
            else
            {
                MessageService.SafeShowMessageBox(
                    message ?? string.Empty, title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);
            }
        }

        /// <inheritdoc/>
        public void ShowError(string message, string title = null)
        {
            MessageService.SafeShowMessageBox(
                message ?? string.Empty,
                title ?? PwDefs.ShortProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
        }

        /// <inheritdoc/>
        public void ShowFatal(string message, string title = null)
        {
            // ShowFatal prepends its own header text and copies to clipboard.
            // We pass the message through the existing method to preserve that.
            MessageService.ShowFatal(message ?? string.Empty);
        }

        /// <inheritdoc/>
        public bool AskYesNo(string question, string title = null, bool defaultToYes = true)
        {
            return MessageService.AskYesNo(
                question ?? string.Empty,
                title ?? PwDefs.ShortProductName,
                defaultToYes);
        }
    }
}
