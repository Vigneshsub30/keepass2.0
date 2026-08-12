using System.Resources;

using KeePass.App.Configuration;
using KeePass.DataExchange;
using KeePass.Ecas;
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
	/// Modern plugin host interface (v2). Replaces the
	/// <see cref="IPluginHost.MainWindow"/> property (typed as
	/// <see cref="IMainWindowService"/>) with
	/// <see cref="ApplicationHost"/> (typed as <see cref="IApplicationHost"/>),
	/// which exposes UI-threading helpers absent from the original contract.
	/// All other members are identical to <see cref="IPluginHost"/>.
	/// </summary>
	public interface IPluginHostV2
	{
		/// <summary>
		/// Platform-neutral application host replacing the legacy
		/// <see cref="IPluginHost.MainWindow"/> property.
		/// </summary>
		IApplicationHost ApplicationHost { get; }

		// ── All other members are identical to IPluginHost ─────────── //

		PwDatabase Database { get; }
		CommandLineArgs CommandLineArgs { get; }
		AceCustomConfig CustomConfig { get; }
		CipherPool CipherPool { get; }
		KeyProviderPool KeyProviderPool { get; }
		KeyValidatorPool KeyValidatorPool { get; }
		FileFormatPool FileFormatPool { get; }
		TempFilesPool TempFilesPool { get; }
		EcasPool EcasPool { get; }
		EcasTriggerSystem TriggerSystem { get; }
		CustomPwGeneratorPool PwGeneratorPool { get; }
		ColumnProviderPool ColumnProviderPool { get; }
		ResourceManager Resources { get; }
	}
}
