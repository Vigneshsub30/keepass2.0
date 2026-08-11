using System;

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
        /// <remarks>
        /// Returns <c>false</c> on the Cinnamon desktop environment, where the
        /// always-on-top window flag is silently ignored by the window manager
        /// (replaces MonoWorkarounds #1716).  Returns <c>true</c> on all other
        /// Linux DEs.
        /// </remarks>
        public bool SupportsAlwaysOnTop { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Always <c>true</c> on Linux — some desktop environments do not
        /// enforce minimum window dimensions set by the application
        /// (replaces MonoWorkarounds #686017).
        /// </remarks>
        public bool RequiresWindowMinSizeEnforcement => true;

        /// <inheritdoc/>
        public PlatformCapabilityTier GetCapabilityTier(PlatformCapability capability)
        {
            switch(capability)
            {
                case PlatformCapability.Clipboard:
                    // Wayland without wlr-data-control = write-only.
                    return IsWayland()
                        ? PlatformCapabilityTier.Partial
                        : PlatformCapabilityTier.Full;
                case PlatformCapability.ClipboardPrivacyMarkers:
                    // Wayland compositors with ext-data-control may support this.
                    return IsWayland()
                        ? PlatformCapabilityTier.Partial
                        : PlatformCapabilityTier.Unsupported;
                case PlatformCapability.CredentialStore:         return PlatformCapabilityTier.Partial; // libsecret
                case PlatformCapability.AutoType:                return PlatformCapabilityTier.Unsupported;
                case PlatformCapability.SecureDesktop:           return PlatformCapabilityTier.Unsupported;
                case PlatformCapability.ScreenCaptureProtection: return PlatformCapabilityTier.Unsupported;
                case PlatformCapability.ProcessDacl:             return PlatformCapabilityTier.Unsupported;
                case PlatformCapability.GlobalHotKeys:           return PlatformCapabilityTier.Partial;
                default:                                         return PlatformCapabilityTier.Unsupported;
            }
        }

        private static bool IsWayland()
        {
            string display = System.Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? string.Empty;
            return display.Length > 0;
        }

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
            bool supportsAlwaysOnTop,
            IClipboardService clipboard,
            ICredentialStore credentialStore,
            IAutoTypeService autoType,
            IScreenProtectionService screenProtection)
        {
            SupportsAlwaysOnTop = supportsAlwaysOnTop;
            Clipboard           = clipboard;
            CredentialStore     = credentialStore;
            AutoType            = autoType;
            ScreenProtection    = screenProtection;
        }

        /// <summary>
        /// Creates and returns a fully wired Linux platform integration.
        /// Should only be called when <c>RuntimeInformation.IsOSPlatform(OSPlatform.Linux)</c>
        /// is true.
        /// </summary>
        public static LinuxPlatformIntegration Create()
        {
            return new LinuxPlatformIntegration(
                supportsAlwaysOnTop: !IsCinnamonDesktop(),
                clipboard:           new LinuxClipboardService(),
                credentialStore:     new LinuxSecretStore(),
                autoType:            new NullAutoTypeService(),
                screenProtection:    new NullScreenProtectionService());
        }

        /// <summary>
        /// Returns <c>true</c> when the current desktop session is Cinnamon.
        /// Checks <c>XDG_CURRENT_DESKTOP</c> for "X-Cinnamon" and
        /// <c>GDMSESSION</c> / <c>DESKTOP_SESSION</c> for "cinnamon",
        /// mirroring the detection logic that was previously in
        /// <c>NativeLib.GetDesktopType()</c>.
        /// </summary>
        private static bool IsCinnamonDesktop()
        {
            string xdg = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty).Trim();
            if(xdg.Equals("X-Cinnamon", StringComparison.OrdinalIgnoreCase)) return true;

            string gdm = (Environment.GetEnvironmentVariable("GDMSESSION") ?? string.Empty).Trim();
            if(gdm.Equals("cinnamon", StringComparison.OrdinalIgnoreCase)) return true;

            string ds = (Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? string.Empty).Trim();
            if(ds.Equals("cinnamon", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}
