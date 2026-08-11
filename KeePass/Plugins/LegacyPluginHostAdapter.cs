using System;
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

namespace KeePass.Plugins
{
	/// <summary>
	/// Wraps an <see cref="IPluginHostV2"/> and presents it as the legacy
	/// <see cref="IPluginHost"/> interface so existing plugins compiled against
	/// <see cref="IPluginHost"/> continue to work without recompilation.
	/// </summary>
	/// <remarks>
	/// The <see cref="MainWindow"/> property is backed by
	/// <see cref="WinFormsApplicationHost.MainWindowService"/>.  On non-WinForms
	/// platforms (where the <see cref="IApplicationHost"/> is not a
	/// <see cref="WinFormsApplicationHost"/>), accessing <see cref="MainWindow"/>
	/// throws <see cref="PlatformNotSupportedException"/> with a descriptive
	/// message telling the plugin author to migrate to <see cref="IPluginHostV2"/>.
	/// </remarks>
	public sealed class LegacyPluginHostAdapter : IPluginHost
	{
		private readonly IPluginHostV2 _v2;

		public LegacyPluginHostAdapter(IPluginHostV2 v2)
			=> _v2 = v2 ?? throw new ArgumentNullException(nameof(v2));

		public IMainWindowService MainWindow
		{
			get
			{
				if (_v2.ApplicationHost is WinFormsApplicationHost wfh)
					return wfh.MainWindowService;

				throw new PlatformNotSupportedException(
					$"IPluginHost.MainWindow is not available on platform " +
					$"'{_v2.ApplicationHost.PlatformName}'. " +
					$"Migrate to IPluginHostV2.ApplicationHost.");
			}
		}

		// ── Delegate all other properties ────────────────────────────── //

		public PwDatabase            Database          => _v2.Database;
		public CommandLineArgs        CommandLineArgs   => _v2.CommandLineArgs;
		public AceCustomConfig        CustomConfig      => _v2.CustomConfig;
		public CipherPool             CipherPool        => _v2.CipherPool;
		public KeyProviderPool        KeyProviderPool   => _v2.KeyProviderPool;
		public KeyValidatorPool       KeyValidatorPool  => _v2.KeyValidatorPool;
		public FileFormatPool         FileFormatPool    => _v2.FileFormatPool;
		public TempFilesPool          TempFilesPool     => _v2.TempFilesPool;
		public EcasPool               EcasPool          => _v2.EcasPool;
		public EcasTriggerSystem      TriggerSystem     => _v2.TriggerSystem;
		public CustomPwGeneratorPool  PwGeneratorPool   => _v2.PwGeneratorPool;
		public ColumnProviderPool     ColumnProviderPool => _v2.ColumnProviderPool;
		public ResourceManager        Resources         => _v2.Resources;
	}
}
