namespace KeePass.Core.Platform
{
    /// <summary>
    /// Single aggregation point for all platform-dependent services.
    ///
    /// Implementations are registered with the DI container at startup (see WO-033).
    /// All sub-services expose an <c>IsSupported</c> capability flag so callers
    /// can query availability without invoking platform APIs directly.
    ///
    /// This interface replaces the scattered static calls to
    /// <c>NativeLib.IsUnix()</c> and <c>MonoWorkarounds.IsRequired()</c>
    /// that are being retired in EPIC-03.
    /// </summary>
    public interface IPlatformIntegration
    {
        /// <summary>
        /// Gets the identifier of the current host operating system.
        /// Determined once at application startup; does not change during the
        /// session.
        /// </summary>
        PlatformId PlatformId { get; }

        /// <summary>Clipboard read/write operations.</summary>
        IClipboardService Clipboard { get; }

        /// <summary>OS-native credential caching (Keychain, Credential Manager, libsecret).</summary>
        ICredentialStore CredentialStore { get; }

        /// <summary>
        /// Keyboard auto-type injection.  Windows-only in v1;
        /// <see cref="IAutoTypeService.IsSupported"/> returns false on other platforms.
        /// </summary>
        IAutoTypeService AutoType { get; }

        /// <summary>
        /// Screen-capture protection.  Available on Windows and macOS;
        /// <see cref="IScreenProtectionService.IsSupported"/> returns false on Linux.
        /// </summary>
        IScreenProtectionService ScreenProtection { get; }
    }
}
