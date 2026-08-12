using KeePass.App.Configuration;

using KeePassLib.Native;

namespace KeePass.Services
{
	/// <summary>
	/// Default implementation of <see cref="IPlatformWorkaroundService"/>.
	/// Applies the Unix/non-Windows config adjustments that were previously
	/// inline in <c>AppConfigEx.OnLoad()</c>.
	/// </summary>
	public sealed class PlatformWorkaroundService : IPlatformWorkaroundService
	{
		// Keys.None == 0; use the literal to avoid a System.Windows.Forms
		// dependency in this cross-platform service.
		private const long NoHotKey = 0L;

		/// <summary>Singleton for use-sites without a DI container.</summary>
		public static readonly PlatformWorkaroundService Instance =
			new PlatformWorkaroundService();

		/// <inheritdoc/>
		public void ApplyConfigWorkarounds(AppConfigEx config)
		{
			if(config == null) return;
			if(!NativeLib.IsUnix()) return;

			// On Linux/macOS, disable Windows-only security and HotKey options
			// that either cause errors or have no effect on non-Windows platforms.
			AceSecurity    aceSec = config.Security;
			AceIntegration aceInt = config.Integration;

			aceSec.PreventScreenCapture     = false;
			aceSec.MasterKeyOnSecureDesktop = false;

			aceInt.HotKeyGlobalAutoType         = NoHotKey;
			aceInt.HotKeyGlobalAutoTypePassword = NoHotKey;
			aceInt.HotKeySelectedAutoType       = NoHotKey;
			aceInt.HotKeyShowWindow             = NoHotKey;
			aceInt.HotKeyEntryMenu              = NoHotKey;
		}
	}
}
