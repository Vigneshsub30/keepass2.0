using KeePass.App.Configuration;

namespace KeePass.Services
{
	/// <summary>
	/// Applies platform-specific configuration adjustments that must run
	/// after the configuration file is deserialized.
	/// Moving these adjustments out of <c>AppConfigEx.OnLoad()</c> prevents
	/// config-layer code from taking a direct dependency on platform-detection
	/// utilities, eliminating the layer violation flagged by ForgeScore.
	/// </summary>
	public interface IPlatformWorkaroundService
	{
		/// <summary>
		/// Applies platform-specific overrides to <paramref name="config"/>
		/// (e.g. disabling Windows-only hot-keys and secure-desktop options
		/// when running on Linux/macOS).
		/// </summary>
		void ApplyConfigWorkarounds(AppConfigEx config);
	}
}
