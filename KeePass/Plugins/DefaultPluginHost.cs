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
using System.Diagnostics;
using System.Resources;

using KeePass.App.Configuration;
using KeePass.DataExchange;
using KeePass.Ecas;
using KeePass.Forms;
using KeePass.UI;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Keys;
using KeePassLib.Plugins;
using KeePassLib.Security;

namespace KeePass.Plugins
{
	internal sealed class DefaultPluginHost : IPluginHost, IPluginHostV2
	{
		// IMainFormFacade instead of concrete MainForm breaks the direct
		// coupling; the cast in WinFormsApplicationHost preserves the WinForms
		// integration for the MainWindow backward-compatibility property.
		private KeePass.Services.IMainFormFacade m_facade = null;
		private WinFormsApplicationHost m_applicationHost = null;
		private CommandLineArgs m_cmdLineArgs = null;
		private CipherPool m_cipherPool = null;

		public DefaultPluginHost()
		{
		}

		/// <param name="facade">
		/// In the WinForms head this is the <c>MainForm</c> instance (which
		/// implements <c>IMainFormFacade</c>).  Avalonia or test hosts may
		/// supply a different implementation.
		/// </param>
		public void Initialize(KeePass.Services.IMainFormFacade facade,
			CommandLineArgs cmdLineArgs, CipherPool cipherPool)
		{
			Debug.Assert(facade != null);
			Debug.Assert(cmdLineArgs != null);
			Debug.Assert(cipherPool != null);

			m_facade = facade;
			// WinFormsApplicationHost still needs the concrete MainForm for
			// WinForms-specific UI calls.  The cast is safe because in the
			// WinForms head the facade IS the MainForm.
			m_applicationHost = facade is MainForm mf
				? new WinFormsApplicationHost(mf)
				: null;
			m_cmdLineArgs = cmdLineArgs;
			m_cipherPool  = cipherPool;
		}

		// ── IPluginHost (legacy) ────────────────────────────────────── //

		public IMainWindowService MainWindow
		{
			get { return m_applicationHost?.MainWindowService; }
		}

		// ── IPluginHostV2 (modern) ──────────────────────────────────── //

		public IApplicationHost ApplicationHost
		{
			get { return m_applicationHost; }
		}

		public PwDatabase Database
		{
			get { return m_facade?.ActiveDatabase; }
		}

		public CommandLineArgs CommandLineArgs
		{
			get { return m_cmdLineArgs; }
		}

		public AceCustomConfig CustomConfig
		{
			get { return Program.Config.CustomConfig; }
		}

		public CipherPool CipherPool
		{
			get { return m_cipherPool; }
		}

		public KeyProviderPool KeyProviderPool
		{
			get { return Program.KeyProviderPool; }
		}

		public KeyValidatorPool KeyValidatorPool
		{
			get { return Program.KeyValidatorPool; }
		}

		public FileFormatPool FileFormatPool
		{
			get { return Program.FileFormatPool; }
		}

		public TempFilesPool TempFilesPool
		{
			get { return Program.TempFilesPool; }
		}

		public EcasPool EcasPool
		{
			get { return Program.EcasPool; }
		}

		public EcasTriggerSystem TriggerSystem
		{
			get { return Program.TriggerSystem; }
		}

		public CustomPwGeneratorPool PwGeneratorPool
		{
			get { return Program.PwGeneratorPool; }
		}

		public ColumnProviderPool ColumnProviderPool
		{
			get { return Program.ColumnProviderPool; }
		}

		public ResourceManager Resources
		{
			get { return Properties.Resources.ResourceManager; }
		}
	}
}
