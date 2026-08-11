namespace KeePass.Core.Services
{
    /// <summary>
    /// Platform-neutral abstraction for file-system dialogs, text-input prompts,
    /// and task dialogs.
    ///
    /// Implementations delegate to the host UI framework:
    ///   WinForms  → <see cref="OpenFileDialog"/>, <see cref="SaveFileDialog"/>,
    ///               <c>VistaTaskDialog</c>, and the KeePass <c>SingleLineEditForm</c>.
    ///   Avalonia  → will be added in a future work order.
    ///
    /// All string-returning methods return <c>null</c> when the user cancels
    /// the dialog, and a non-null string (possibly empty) on confirmation.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a file-open dialog and returns the selected file path,
        /// or <c>null</c> if the user cancelled.
        /// </summary>
        /// <param name="title">Dialog title shown in the title bar.</param>
        /// <param name="filter">
        /// File-type filter string in the format accepted by
        /// <c>System.Windows.Forms.FileDialog.Filter</c>:
        /// e.g. <c>"KeePass Databases (*.kdbx)|*.kdbx|All Files (*.*)|*.*"</c>.
        /// Pass <c>null</c> to show all files.
        /// </param>
        /// <param name="initialDirectory">
        /// Starting directory. <c>null</c> uses the OS default.
        /// </param>
        string ShowOpenFileDialog(string title, string filter = null,
            string initialDirectory = null);

        /// <summary>
        /// Shows a file-save dialog and returns the chosen file path,
        /// or <c>null</c> if the user cancelled.
        /// </summary>
        string ShowSaveFileDialog(string title, string filter = null,
            string initialDirectory = null, string defaultFileName = null);

        /// <summary>
        /// Shows a single-line text-input dialog and returns the entered text,
        /// or <c>null</c> if the user cancelled.
        /// </summary>
        /// <param name="prompt">Instruction shown above the text field.</param>
        /// <param name="title">Dialog title. <c>null</c> uses the default.</param>
        /// <param name="initialValue">Pre-populated value. <c>null</c> for empty.</param>
        string ShowInputDialog(string prompt, string title = null,
            string initialValue = null);

        /// <summary>
        /// Shows a task dialog described by <paramref name="model"/>.
        /// Returns the zero-based index of the button the user clicked,
        /// or <c>-1</c> if the dialog was cancelled (e.g. via Escape or the
        /// close button when <c>AllowDialogCancellation</c> is set).
        ///
        /// When <paramref name="model"/> contains a
        /// <see cref="TaskDialogModel.VerificationText"/>, the
        /// <see cref="TaskDialogModel.VerificationResult"/> property on
        /// <paramref name="model"/> is updated before this method returns.
        /// </summary>
        int ShowTaskDialog(TaskDialogModel model);
    }
}
