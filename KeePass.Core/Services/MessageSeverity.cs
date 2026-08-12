namespace KeePass.Core.Services
{
    /// <summary>
    /// Indicates the nature/severity of a message shown via
    /// <see cref="IMessageService"/>.
    /// Replaces <c>System.Windows.Forms.MessageBoxIcon</c> in cross-platform code.
    /// </summary>
    public enum MessageSeverity
    {
        /// <summary>Informational message; no action required.</summary>
        Info = 0,

        /// <summary>Warning — the user should be aware but the operation can continue.</summary>
        Warning = 1,

        /// <summary>Error — an operation failed.</summary>
        Error = 2,

        /// <summary>
        /// Fatal / unrecoverable error.
        /// Implementations typically copy the error details to the clipboard
        /// and show a prominent dialog.
        /// </summary>
        Fatal = 3,
    }
}
