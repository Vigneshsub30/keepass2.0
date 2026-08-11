using System;

namespace KeePass.Core.Platform
{
    /// <summary>
    /// Provides cross-platform clipboard operations for password and field data.
    ///
    /// Implementations must be safe to call on any thread and must not throw
    /// for operations when <see cref="IsSupported"/> is true.
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>
        /// Gets a value indicating whether clipboard access is available on
        /// the current platform.  Callers must check this before calling any
        /// other member; calling an unsupported member throws
        /// <see cref="PlatformNotSupportedException"/>.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Copies <paramref name="text"/> to the system clipboard.
        /// </summary>
        /// <param name="text">Text to copy. Must not be null.</param>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if <see cref="IsSupported"/> is false.
        /// </exception>
        void SetText(string text);

        /// <summary>
        /// Returns the current text content of the system clipboard, or
        /// <c>null</c> if the clipboard is empty or contains non-text content.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if <see cref="IsSupported"/> is false.
        /// </exception>
        string GetText();

        /// <summary>Clears all data from the system clipboard.</summary>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if <see cref="IsSupported"/> is false.
        /// </exception>
        void Clear();

        /// <summary>
        /// Clears the clipboard only if the text currently on it was placed
        /// there by this application.  No-op if the clipboard was changed by
        /// another process after the last <see cref="SetText"/> call.
        /// </summary>
        void ClearIfOwner();

        /// <summary>
        /// Copies <paramref name="text"/> to the clipboard and schedules an
        /// automatic clear after <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="text">Text to copy.</param>
        /// <param name="timeout">Duration after which the clipboard is cleared.</param>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if <see cref="IsSupported"/> is false.
        /// </exception>
        void SetWithAutoClear(string text, TimeSpan timeout);
    }
}
