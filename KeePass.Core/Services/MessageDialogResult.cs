namespace KeePass.Core.Services
{
    /// <summary>
    /// Platform-neutral result returned by dialog and message-box methods.
    /// Replaces <c>System.Windows.Forms.DialogResult</c> in cross-platform code.
    /// </summary>
    public enum MessageDialogResult
    {
        /// <summary>The user clicked OK (or the only available button).</summary>
        OK = 0,

        /// <summary>The user cancelled or dismissed the dialog without confirming.</summary>
        Cancel = 1,

        /// <summary>The user clicked Yes in a Yes/No dialog.</summary>
        Yes = 2,

        /// <summary>The user clicked No in a Yes/No dialog.</summary>
        No = 3,

        /// <summary>The user clicked Abort in an Abort/Retry/Ignore dialog.</summary>
        Abort = 4,

        /// <summary>The user clicked Retry in an Abort/Retry/Ignore dialog.</summary>
        Retry = 5,
    }
}
