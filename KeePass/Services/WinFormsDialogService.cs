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
using System.Drawing;
using System.Windows.Forms;

using KeePass.Core.Services;
using KeePass.Forms;
using KeePass.UI;
using KeePassLib;

namespace KeePass.Services
{
    /// <summary>
    /// WinForms implementation of <see cref="IDialogService"/>.
    ///
    /// Delegates to:
    /// <list type="bullet">
    ///   <item><see cref="OpenFileDialog"/> / <see cref="SaveFileDialog"/> for file picking.</item>
    ///   <item><see cref="SingleLineEditForm"/> for text input prompts.</item>
    ///   <item><see cref="VistaTaskDialog"/> for task dialogs (Windows Vista+ native style).</item>
    /// </list>
    /// </summary>
    public sealed class WinFormsDialogService : IDialogService
    {
        /// <inheritdoc/>
        public string ShowOpenFileDialog(string title, string filter = null,
            string initialDirectory = null)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title       = title ?? string.Empty;
                dlg.Filter      = filter ?? "All Files (*.*)|*.*";
                dlg.Multiselect = false;

                if (!string.IsNullOrEmpty(initialDirectory))
                    dlg.InitialDirectory = initialDirectory;

                if (dlg.ShowDialog() == DialogResult.OK)
                    return dlg.FileName;
            }
            return null;
        }

        /// <inheritdoc/>
        public string ShowSaveFileDialog(string title, string filter = null,
            string initialDirectory = null, string defaultFileName = null)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title  = title ?? string.Empty;
                dlg.Filter = filter ?? "All Files (*.*)|*.*";

                if (!string.IsNullOrEmpty(initialDirectory))
                    dlg.InitialDirectory = initialDirectory;

                if (!string.IsNullOrEmpty(defaultFileName))
                    dlg.FileName = defaultFileName;

                if (dlg.ShowDialog() == DialogResult.OK)
                    return dlg.FileName;
            }
            return null;
        }

        /// <inheritdoc/>
        public string ShowInputDialog(string prompt, string title = null,
            string initialValue = null)
        {
            // Use the system Information icon as the banner graphic.
            // This is always available in WinForms and gives a neutral appearance.
            Image imgIcon = SystemIcons.Information.ToBitmap();

            SingleLineEditForm f = new SingleLineEditForm();
            f.InitEx(
                title ?? PwDefs.ShortProductName,
                prompt ?? string.Empty,
                string.Empty,        // long description
                imgIcon,
                initialValue ?? string.Empty,
                null);               // selectable items

            DialogResult dr = UIUtil.ShowDialogAndDestroy(f);
            imgIcon.Dispose();

            return (dr == DialogResult.OK) ? f.ResultString : null;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Falls back to a standard <see cref="MessageBox"/> when running on
        /// Windows versions before Vista, or when the task dialog API fails.
        /// </remarks>
        public int ShowTaskDialog(TaskDialogModel model)
        {
            if (model == null) throw new ArgumentNullException("model");

            string[] buttons = model.Buttons;
            if (buttons == null || buttons.Length == 0)
                buttons = new[] { "OK" };

            VistaTaskDialog dlg = new VistaTaskDialog();
            dlg.MainInstruction = model.MainInstruction ?? string.Empty;
            dlg.Content         = model.Content ?? string.Empty;
            dlg.CommandLinks    = model.UseCommandLinks;
            dlg.VerificationText = model.VerificationText;

            dlg.SetIcon(SeverityToVtdIcon(model.Severity));

            if (!string.IsNullOrEmpty(model.FooterText))
            {
                dlg.FooterText = model.FooterText;
                dlg.SetFooterIcon(SeverityToVtdIcon(model.FooterSeverity));
            }

            // Add each button; use 1-based IDs to avoid ID = 0 (cancel sentinel)
            for (int i = 0; i < buttons.Length; i++)
                dlg.AddButton(i + 1, buttons[i], null);
            dlg.DefaultButtonID = model.DefaultButtonIndex + 1;

            bool shown = dlg.ShowDialog();
            if (!shown)
            {
                // VistaTaskDialog not available — fall back to MessageBox
                return ShowFallbackDialog(model, buttons);
            }

            if (model.VerificationText != null)
                model.VerificationResult = dlg.ResultVerificationChecked;

            int resultId = dlg.Result;
            return (resultId > 0 && resultId <= buttons.Length)
                ? resultId - 1  // convert back to 0-based
                : -1;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static VtdIcon SeverityToVtdIcon(MessageSeverity severity)
        {
            switch (severity)
            {
                case MessageSeverity.Warning: return VtdIcon.Warning;
                case MessageSeverity.Error:   return VtdIcon.Error;
                case MessageSeverity.Fatal:   return VtdIcon.Error;
                default:                      return VtdIcon.Information;
            }
        }

        private static int ShowFallbackDialog(TaskDialogModel model, string[] buttons)
        {
            // Fallback: show a MessageBox with the main instruction and content.
            string text = string.IsNullOrEmpty(model.MainInstruction)
                ? model.Content ?? string.Empty
                : (model.Content == null
                    ? model.MainInstruction
                    : model.MainInstruction + Environment.NewLine + model.Content);

            MessageBoxIcon icon;
            switch (model.Severity)
            {
                case MessageSeverity.Warning: icon = MessageBoxIcon.Warning; break;
                case MessageSeverity.Error:
                case MessageSeverity.Fatal:   icon = MessageBoxIcon.Error;   break;
                default:                      icon = MessageBoxIcon.Information; break;
            }

            // Only 2-button fall-back options are supported via MessageBox.
            if (buttons.Length >= 2)
            {
                DialogResult dr = MessageBox.Show(text, PwDefs.ShortProductName,
                    MessageBoxButtons.YesNo, icon,
                    model.DefaultButtonIndex == 0
                        ? MessageBoxDefaultButton.Button1
                        : MessageBoxDefaultButton.Button2);
                return (dr == DialogResult.Yes) ? 0 : 1;
            }

            MessageBox.Show(text, PwDefs.ShortProductName,
                MessageBoxButtons.OK, icon);
            return 0;
        }
    }
}
