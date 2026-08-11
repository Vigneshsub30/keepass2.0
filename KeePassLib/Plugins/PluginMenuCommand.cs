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
using System.Collections.Generic;

namespace KeePassLib.Plugins
{
	/// <summary>
	/// Platform-neutral representation of a plugin menu item.
	/// The UI layer is responsible for converting this DTO into the
	/// platform-specific menu type (e.g. <c>ToolStripMenuItem</c> on WinForms,
	/// <c>MenuItem</c> on Avalonia).
	/// </summary>
	public sealed class PluginMenuCommand
	{
		/// <summary>Text shown in the menu.</summary>
		public string Text { get; set; }

		/// <summary>
		/// Optional tooltip shown when the user hovers over the item.
		/// <c>null</c> means no tooltip.
		/// </summary>
		public string ToolTipText { get; set; }

		/// <summary>
		/// Whether the item is enabled (clickable).  Defaults to <c>true</c>.
		/// </summary>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// Whether the item is checked (has a checkmark).  Defaults to <c>false</c>.
		/// </summary>
		public bool Checked { get; set; }

		/// <summary>
		/// Whether the item acts as a separator.  When <c>true</c>, all
		/// other properties are ignored.
		/// </summary>
		public bool IsSeparator { get; set; }

		/// <summary>
		/// Raw icon data for the menu item.  May be <c>null</c> when no icon
		/// is provided.  The UI layer decodes this according to <see cref="ImageFormat"/>.
		/// </summary>
		public byte[] ImageData { get; set; }

		/// <summary>Image format for <see cref="ImageData"/>.</summary>
		public string ImageFormat { get; set; }

		/// <summary>
		/// Invoked on the UI thread when the user activates the menu item.
		/// </summary>
		public EventHandler Click { get; set; }

		/// <summary>
		/// Optional sub-commands (dropdown items).  An empty list means
		/// no sub-menu.
		/// </summary>
		public IList<PluginMenuCommand> SubItems { get; set; } =
			new List<PluginMenuCommand>();

		/// <summary>
		/// Creates a regular menu command with the given text and click handler.
		/// </summary>
		public PluginMenuCommand(string text, EventHandler click = null)
		{
			Text = text ?? throw new ArgumentNullException("text");
			Click = click;
		}

		/// <summary>
		/// Creates a separator item.
		/// </summary>
		public static PluginMenuCommand Separator()
		{
			return new PluginMenuCommand("-") { IsSeparator = true };
		}
	}
}
