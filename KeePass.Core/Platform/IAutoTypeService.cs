namespace KeePass.Core.Platform
{
    /// <summary>
    /// Performs auto-type keyboard injection on behalf of the application.
    ///
    /// Auto-type is Windows-only in v1 (requires SendInput / SendKeys and
    /// global hotkey registration).  On macOS and Linux <see cref="IsSupported"/>
    /// returns false; calling <see cref="PerformAutoType"/> throws
    /// <see cref="System.PlatformNotSupportedException"/> on those platforms.
    ///
    /// Future versions may support macOS (AppleScript) and X11 (XSendEvent).
    /// </summary>
    public interface IAutoTypeService
    {
        /// <summary>
        /// Gets a value indicating whether keyboard auto-type injection is
        /// supported on the current platform.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Sends the key sequence described by <paramref name="ctx"/> to the
        /// active foreground window.
        /// </summary>
        /// <param name="ctx">Context carrying the sequence and optional target hint.</param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if <paramref name="ctx"/> is null.
        /// </exception>
        /// <exception cref="System.PlatformNotSupportedException">
        /// Thrown on platforms where <see cref="IsSupported"/> is false.
        /// </exception>
        void PerformAutoType(AutoTypeContext ctx);
    }
}
