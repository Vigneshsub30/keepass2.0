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
    /// that are being retired in EPIC-03 (WO-035).
    ///
    /// <para><b>MonoWorkarounds retirement classification (WO-035):</b></para>
    /// <list type="table">
    ///   <item><term>RETIRE</term><description>
    ///     All numbered Mono bug workarounds (106, 1219, 1245, 1254, 1354, 1358,
    ///     1366, 1378, 1418, 1468, 1527, 1530, 1574, 1613, 1632, 1690, 1710,
    ///     1760, 1976, 2140, 2247, 5795, 9604, 10163, 12525, 19836, 100001–100004,
    ///     190417, 373134, 586901, 620618, 649266, 688007, 801414, 891029,
    ///     836428016, 2449941153, 3471228285, 3574233558, 4190280862).
    ///     All were Mono-runtime defects that do not exist in .NET 10.
    ///   </description></item>
    ///   <item><term>RE-IMPLEMENT</term><description>
    ///     1716 – always-on-top broken on Cinnamon desktop →
    ///       <see cref="SupportsAlwaysOnTop"/>.
    ///     686017 – minimum window sizes must be enforced on some Linux DEs →
    ///       <see cref="RequiresWindowMinSizeEnforcement"/>.
    ///   </description></item>
    ///   <item><term>OBSOLETE</term><description>
    ///     1530 / 1613 – clipboard workarounds; already returned false in the
    ///     original code. Replaced by <see cref="IClipboardService.IsSupported"/>.
    ///   </description></item>
    /// </list>
    /// </summary>
    public interface IPlatformIntegration
    {
        /// <summary>
        /// Gets the identifier of the current host operating system.
        /// Determined once at application startup; does not change during the
        /// session.
        /// </summary>
        PlatformId PlatformId { get; }

        /// <summary>
        /// Returns <c>true</c> when the window manager reliably honours the
        /// always-on-top (top-most) window flag.
        ///
        /// <para>Returns <c>false</c> on Linux Cinnamon desktop environments
        /// where the flag is silently ignored (replaces MonoWorkarounds #1716).
        /// Returns <c>true</c> on Windows, macOS, and all other Linux DEs.</para>
        /// </summary>
        bool SupportsAlwaysOnTop { get; }

        /// <summary>
        /// Returns <c>true</c> when the UI layer must enforce minimum window
        /// dimensions manually because the window manager does not do so.
        ///
        /// <para>Returns <c>true</c> on Linux (replaces MonoWorkarounds #686017).
        /// Returns <c>false</c> on Windows and macOS.</para>
        /// </summary>
        bool RequiresWindowMinSizeEnforcement { get; }

        /// <summary>
        /// Returns the availability tier for a discrete platform capability.
        ///
        /// <para>Callers use this to degrade gracefully when a feature is not
        /// fully supported on the current platform.  Implementations must return
        /// <see cref="PlatformCapabilityTier.Unsupported"/> for any
        /// <see cref="PlatformCapability"/> value they do not explicitly handle,
        /// so that future capabilities added to the enum do not cause exceptions
        /// on older builds.</para>
        /// </summary>
        /// <param name="capability">The capability to query.</param>
        /// <returns>The tier describing how well the capability is supported.</returns>
        PlatformCapabilityTier GetCapabilityTier(PlatformCapability capability);

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
