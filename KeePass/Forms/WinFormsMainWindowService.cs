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

using KeePassLib.Plugins;

namespace KeePass.Forms
{
	/// <summary>
	/// WinForms implementation of <see cref="IMainWindowService"/>.
	/// Delegates all operations to the actual <see cref="MainForm"/> instance.
	/// </summary>
	public sealed class WinFormsMainWindowService : IMainWindowService
	{
		private readonly MainForm m_form;

		/// <param name="form">The KeePass main form; must not be <c>null</c>.</param>
		public WinFormsMainWindowService(MainForm form)
		{
			if(form == null) throw new ArgumentNullException("form");
			m_form = form;
		}

		/// <inheritdoc/>
		public bool IsVisible
		{
			get
			{
				if(m_form.IsDisposed) return false;
				return m_form.Visible && !m_form.WindowState.Equals(
					System.Windows.Forms.FormWindowState.Minimized);
			}
		}

		/// <inheritdoc/>
		public void BringToForeground()
		{
			if(m_form.IsDisposed) { Debug.Assert(false); return; }
			m_form.Invoke((System.Windows.Forms.MethodInvoker)delegate
			{
				m_form.BringToFront();
				m_form.Activate();
			});
		}

		/// <inheritdoc/>
		public void RefreshEntryList()
		{
			if(m_form.IsDisposed) { Debug.Assert(false); return; }
			m_form.Invoke((System.Windows.Forms.MethodInvoker)delegate
			{
				m_form.RefreshEntriesList();
			});
		}

		/// <inheritdoc/>
		public void UpdateToolBar()
		{
			if(m_form.IsDisposed) { Debug.Assert(false); return; }
			m_form.Invoke((System.Windows.Forms.MethodInvoker)delegate
			{
				m_form.UpdateUI(false, null, false, null, false, null, false);
			});
		}

		/// <inheritdoc/>
		public bool SaveAllDatabases()
		{
			if(m_form.IsDisposed) { Debug.Assert(false); return false; }

			bool bResult = false;
			m_form.Invoke((System.Windows.Forms.MethodInvoker)delegate
			{
				bResult = m_form.UIFileSave(false);
			});
			return bResult;
		}
	}
}
