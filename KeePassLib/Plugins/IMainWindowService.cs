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

namespace KeePassLib.Plugins
{
	/// <summary>
	/// Platform-neutral interface through which plugins interact with the
	/// KeePass main window.  Concrete implementations wrap the actual UI
	/// window type (e.g. <c>MainForm</c> on WinForms).
	/// </summary>
	public interface IMainWindowService
	{
		/// <summary>Activates and brings the main window to the foreground.</summary>
		void BringToForeground();

		/// <summary>Refreshes the visible entry list.</summary>
		void RefreshEntryList();

		/// <summary>Updates the main toolbar and status bar.</summary>
		void UpdateToolBar();

		/// <summary>
		/// Saves all open databases.
		/// </summary>
		/// <returns><c>true</c> if all databases were saved successfully.</returns>
		bool SaveAllDatabases();

		/// <summary>
		/// Whether the main window is currently visible (not minimized/hidden).
		/// </summary>
		bool IsVisible { get; }
	}
}
