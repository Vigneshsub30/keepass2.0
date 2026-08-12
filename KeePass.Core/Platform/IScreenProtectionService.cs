namespace KeePass.Core.Platform
{
    /// <summary>
    /// Prevents the application's windows from being captured by screen-recording
    /// or screenshot APIs.
    ///
    /// Platform implementations:
    /// - Windows: SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)
    /// - macOS:   NSWindow.sharingType = NSWindowSharingNone
    /// - Linux:   Not available in v1 (<see cref="IsSupported"/> returns false).
    /// </summary>
    public interface IScreenProtectionService
    {
        /// <summary>
        /// Gets a value indicating whether screen-capture protection is
        /// supported on the current platform.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Enables screen-capture protection for the main application window.
        /// No-op if already enabled or if <see cref="IsSupported"/> is false.
        /// </summary>
        void Enable();

        /// <summary>
        /// Disables screen-capture protection and restores normal window visibility.
        /// No-op if already disabled or if <see cref="IsSupported"/> is false.
        /// </summary>
        void Disable();
    }
}
