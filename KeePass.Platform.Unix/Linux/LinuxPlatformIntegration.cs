using KeePass.Core.Platform;

namespace KeePass.Platform.Unix.Linux
{
    /// <summary>
    /// Aggregates all Linux-specific platform-service implementations into a
    /// single <see cref="IPlatformIntegration"/> instance.
    ///
    /// Auto-type (XSendEvent/XTest) and screen-capture protection are not
    /// available on Linux in v1; both fall back to their null stubs.
    ///
    /// Use the <see cref="Create"/> factory method to obtain an instance.
    /// </summary>
    public sealed class LinuxPlatformIntegration : IPlatformIntegration
    {
        /// <inheritdoc/>
        public PlatformId PlatformId => PlatformId.Linux;

        /// <inheritdoc/>
        public IClipboardService Clipboard { get; }

        /// <inheritdoc/>
        public ICredentialStore CredentialStore { get; }

        /// <inheritdoc/>
        /// <remarks>Returns a <see cref="NullAutoTypeService"/> — not available on Linux in v1.</remarks>
        public IAutoTypeService AutoType { get; }

        /// <inheritdoc/>
        /// <remarks>Returns a <see cref="NullScreenProtectionService"/> — not available on Linux.</remarks>
        public IScreenProtectionService ScreenProtection { get; }

        private LinuxPlatformIntegration(
            IClipboardService clipboard,
            ICredentialStore credentialStore,
            IAutoTypeService autoType,
            IScreenProtectionService screenProtection)
        {
            Clipboard        = clipboard;
            CredentialStore  = credentialStore;
            AutoType         = autoType;
            ScreenProtection = screenProtection;
        }

        /// <summary>
        /// Creates and returns a fully wired Linux platform integration.
        /// Should only be called when <c>RuntimeInformation.IsOSPlatform(OSPlatform.Linux)</c>
        /// is true.
        /// </summary>
        public static LinuxPlatformIntegration Create()
        {
            return new LinuxPlatformIntegration(
                clipboard:        new LinuxClipboardService(),
                credentialStore:  new LinuxSecretStore(),
                autoType:         new NullAutoTypeService(),
                screenProtection: new NullScreenProtectionService());
        }
    }
}
