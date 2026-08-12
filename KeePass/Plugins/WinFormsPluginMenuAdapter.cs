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
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using KeePassLib.Plugins;

namespace KeePass.Plugins
{
	/// <summary>
	/// Converts a platform-neutral <see cref="PluginMenuCommand"/> tree into
	/// a WinForms <see cref="ToolStripMenuItem"/> hierarchy.
	/// </summary>
	public static class WinFormsPluginMenuAdapter
	{
		/// <summary>
		/// Creates a <see cref="ToolStripMenuItem"/> from a
		/// <see cref="PluginMenuCommand"/>, recursively converting sub-items.
		/// Returns <c>null</c> when <paramref name="cmd"/> is <c>null</c>.
		/// </summary>
		public static ToolStripMenuItem ToMenuItem(PluginMenuCommand cmd)
		{
			if(cmd == null) return null;

			if(cmd.IsSeparator)
			{
				// Callers usually check for ToolStripSeparator; log and skip.
				Debug.Assert(false, "Use ToolStripSeparator for separator items.");
				return null;
			}

			ToolStripMenuItem tsmi = new ToolStripMenuItem(cmd.Text ?? string.Empty);
			tsmi.Enabled = cmd.Enabled;
			tsmi.Checked = cmd.Checked;

			if(!string.IsNullOrEmpty(cmd.ToolTipText))
				tsmi.ToolTipText = cmd.ToolTipText;

			if(cmd.ImageData != null && cmd.ImageData.Length > 0)
			{
				try
				{
					using(MemoryStream ms = new MemoryStream(cmd.ImageData))
						tsmi.Image = Image.FromStream(ms);
				}
				catch(Exception) { Debug.Assert(false); }
			}

			if(cmd.Click != null)
				tsmi.Click += cmd.Click;

			foreach(PluginMenuCommand sub in cmd.SubItems)
			{
				if(sub == null) continue;
				if(sub.IsSeparator)
					tsmi.DropDownItems.Add(new ToolStripSeparator());
				else
				{
					ToolStripMenuItem child = ToMenuItem(sub);
					if(child != null) tsmi.DropDownItems.Add(child);
				}
			}

			return tsmi;
		}
	}
}
