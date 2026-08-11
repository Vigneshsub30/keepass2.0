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
using System.Text;

using KeePassLib.Plugins;

namespace KeePass.Plugins
{
	/// <summary>
	/// KeePass plugin base class. All KeePass plugins must derive
	/// from this class.
	/// </summary>
	public abstract class Plugin
	{
		/// <summary>
		/// The <c>Initialize</c> method is called by KeePass when
		/// you should initialize your plugin.
		/// </summary>
		/// <param name="host">Plugin host interface. Through this
		/// interface you can access the KeePass main window, the
		/// currently open database, etc.</param>
		/// <returns>You must return <c>true</c> in order to signal
		/// successful initialization. If you return <c>false</c>,
		/// KeePass unloads your plugin (without calling the
		/// <c>Terminate</c> method of your plugin).</returns>
		public virtual bool Initialize(IPluginHost host)
		{
			return (host != null);
		}

		/// <summary>
		/// The <c>Terminate</c> method is called by KeePass when
		/// you should free all resources, close files/streams,
		/// remove event handlers, etc.
		/// </summary>
		public virtual void Terminate()
		{
		}

		/// <summary>
		/// Get raw image data for a small icon representing the plugin.
		/// The decoded icon should be 16×16 device-independent pixels.
		/// Returns <c>null</c> when the plugin provides no icon.
		/// </summary>
		public virtual byte[] SmallIconData
		{
			get { return null; }
		}

		/// <summary>
		/// Get the URL of a version information file. See
		/// https://keepass.info/help/v2_dev/plg_index.html#upd
		/// </summary>
		public virtual string UpdateUrl
		{
			get { return null; }
		}

		/// <summary>
		/// Get a platform-neutral menu command describing the plugin's menu item.
		/// The UI layer converts this into the appropriate platform type
		/// (e.g. <see cref="System.Windows.Forms.ToolStripMenuItem"/>).
		/// Returns <c>null</c> when the plugin provides no menu item for
		/// <paramref name="t"/>.
		/// See https://keepass.info/help/v2_dev/plg_index.html#co_menuitem
		/// </summary>
		/// <param name="t">Type of the menu that the plugin should
		/// return an item for.</param>
		[Obsolete("Override GetMenuCommands instead. GetMenuCommand will be " +
			"removed in a future version.")]
		public virtual PluginMenuCommand GetMenuCommand(PluginMenuType t)
		{
			return null;
		}

		/// <summary>
		/// Returns a list of platform-neutral menu commands for
		/// <paramref name="t"/>.  Override this method to provide menu items
		/// without depending on any WinForms or Avalonia types.
		/// </summary>
		/// <remarks>
		/// The default implementation bridges the legacy
		/// <see cref="GetMenuCommand"/> method: if it returns a non-null value
		/// the result is wrapped in a single-element list.  Plugins that
		/// override <see cref="GetMenuCommands"/> directly need not override
		/// <see cref="GetMenuCommand"/>.
		/// </remarks>
		public virtual IReadOnlyList<PluginMenuCommand> GetMenuCommands(PluginMenuType t)
		{
#pragma warning disable CS0618 // call the obsolete bridge for backward compat
			PluginMenuCommand single = GetMenuCommand(t);
#pragma warning restore CS0618
			if (single == null)
				return Array.Empty<PluginMenuCommand>();

			return new PluginMenuCommand[] { single };
		}
	}

	public enum PluginMenuType
	{
		/// <summary>
		/// Main menu item of the plugin, which KeePass typically
		/// shows in the 'Tools' menu.
		/// </summary>
		Main = 0,

		/// <summary>
		/// Group menu item of the plugin, which KeePass typically
		/// shows in the context menu of a group.
		/// </summary>
		Group,

		/// <summary>
		/// Entry menu item of the plugin, which KeePass typically
		/// shows in the context menu of an entry.
		/// </summary>
		Entry,

		/// <summary>
		/// Tray menu item of the plugin, which KeePass typically
		/// shows in the context menu of its system tray icon.
		/// </summary>
		Tray
	}
}
