using KeePass.Core.Platform;

namespace KeePass.Platform.Unix.Mac
{
    /// <summary>
    /// Aggregates all macOS-specific platform-service implementations into a
    /// single <see cref="IPlatformIntegration"/> instance.
    ///
    /// Auto-type and screen-protection are not yet implemented on macOS;
    /// both are backed by their respective null stubs so the host can query
    /// <see cref="IAutoTypeService.IsSupported"/> and
    /// <see cref="IScreenProtectionService.IsSupported"/> safely.
    ///
    /// Use the <see cref="Create"/> factory method to obtain an instance.
    /// </summary>
    public sealed class MacPlatformIntegration : IPlatformIntegration
    {
        /// <inheritdoc/>
        public PlatformId PlatformId => PlatformId.MacOS;

        /// <inheritdoc/>
        /// <remarks>Always <c>true</c> on macOS — the window manager honours the flag.</remarks>
        public bool SupportsAlwaysOnTop => true;

        /// <inheritdoc/>
        /// <remarks>Always <c>false</c> on macOS — the OS enforces minimum sizes.</remarks>
        public bool RequiresWindowMinSizeEnforcement => false;

        /// <inheritdoc/>
        public IClipboardService Clipboard { get; }

        /// <inheritdoc/>
        public ICredentialStore CredentialStore { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Returns a <see cref="NullAutoTypeService"/> in v1.
        /// macOS auto-type (AppleScript / CGEvent) is planned for a future WO.
        /// </remarks>
        public IAutoTypeService AutoType { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Returns a <see cref="NullScreenProtectionService"/> in v1.
        /// NSWindow.sharingType = NSWindowSharingNone is planned for a future WO.
        /// </remarks>
        public IScreenProtectionService ScreenProtection { get; }

        private MacPlatformIntegration(
            IClipboardService clipboard,
            ICredentialStore credentialStore,
            IAutoTypeService autoType,
            IScreenProtectionService screenProtection)
        {
            Clipboard         = clipboard;
            CredentialStore   = credentialStore;
            AutoType          = autoType;
            ScreenProtection  = screenProtection;
        }

        /// <summary>
        /// Creates and returns a fully wired macOS platform integration.
        /// Should only be called when <c>RuntimeInformation.IsOSPlatform(OSPlatform.OSX)</c>
        /// is true.
        /// </summary>
        public static MacPlatformIntegration Create()
        {
            return new MacPlatformIntegration(
                clipboard:        new MacClipboardService(),
                credentialStore:  new MacKeychainStore(),
                autoType:         new NullAutoTypeService(),
                screenProtection: new NullScreenProtectionService());
        }
    }
}
