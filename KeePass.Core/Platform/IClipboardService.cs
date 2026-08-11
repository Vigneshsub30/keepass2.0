using System;

namespace KeePass.Core.Platform
{
    /// <summary>
    /// Provides cross-platform clipboard operations for password and field data.
    ///
    /// Implementations must be safe to call on any thread and must not throw
    /// for operations when <see cref="IsSupported"/> is true.
    ///
    /// <para>The extended interface members (<see cref="CopyText"/>,
    /// <see cref="CopyData"/>, <see cref="StartAutoClear"/>,
    /// <see cref="StopAutoClear"/>, <see cref="IsAutoClearActive"/>)
    /// were added in WO-040.  All existing implementations inherit default
    /// implementations that delegate to the core methods so no breaking change
    /// is introduced.  New implementations should prefer extending
    /// <see cref="ClipboardServiceBase"/> which wires the ownership hash and
    /// auto-clear timer automatically.</para>
    /// </summary>
    public interface IClipboardService
    {
        // ── Core members (present since WO-026) ───────────────────────────────

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

        // ── Extended members (added in WO-040) ────────────────────────────────

        /// <summary>
        /// Copies <paramref name="text"/> to the system clipboard and
        /// optionally records this process as the clipboard owner (enabling
        /// <see cref="ClearIfOwner"/> semantics).
        ///
        /// <para>Default implementation delegates to <see cref="SetText"/>.</para>
        /// </summary>
        /// <param name="text">Text to copy.  Must not be null.</param>
        /// <param name="setOwnership">
        /// When <c>true</c> the clipboard ownership hash is updated so that
        /// a subsequent <see cref="ClearIfOwner"/> call will clear the content.
        /// </param>
        void CopyText(string text, bool setOwnership) => SetText(text);

        /// <summary>
        /// Copies raw data in an arbitrary clipboard format.  Useful for
        /// setting platform-specific privacy markers alongside text data.
        ///
        /// <para>Default implementation is a no-op; platform providers that
        /// support multiple clipboard formats should override this.</para>
        /// </summary>
        /// <param name="format">
        /// Platform-specific clipboard format name (e.g.
        /// <c>"CanIncludeInClipboardHistory"</c> on Windows).
        /// </param>
        /// <param name="data">Raw bytes to place on the clipboard in this format.</param>
        void CopyData(string format, byte[] data) { /* no-op by default */ }

        /// <summary>
        /// Starts an auto-clear countdown timer that calls
        /// <see cref="ClearIfOwner"/> after <paramref name="seconds"/> have
        /// elapsed.  Any previously running timer is replaced.
        ///
        /// <para>Default implementation calls
        /// <see cref="SetWithAutoClear(string, TimeSpan)"/> with the current
        /// clipboard text.  Override in <see cref="ClipboardServiceBase"/> for
        /// a timer-based implementation that survives external clipboard
        /// changes.</para>
        /// </summary>
        /// <param name="seconds">
        /// Countdown in whole seconds.  0 or negative stops any active timer.
        /// </param>
        void StartAutoClear(int seconds)
        {
            if(seconds <= 0) { StopAutoClear(); return; }
            string text = GetText() ?? string.Empty;
            SetWithAutoClear(text, TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Cancels any active auto-clear timer without clearing the clipboard.
        ///
        /// <para>Default implementation is a no-op.</para>
        /// </summary>
        void StopAutoClear() { /* no-op by default */ }

        /// <summary>
        /// Gets a value indicating whether an auto-clear timer is currently
        /// running (i.e. <see cref="StartAutoClear"/> was called but the timer
        /// has not yet fired and <see cref="StopAutoClear"/> has not been
        /// called).
        ///
        /// <para>Default implementation returns <c>false</c>.</para>
        /// </summary>
        bool IsAutoClearActive => false;
    }
}
