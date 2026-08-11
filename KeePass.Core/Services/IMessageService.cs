namespace KeePass.Core.Services
{
    /// <summary>
    /// Platform-neutral abstraction for displaying simple message dialogs.
    ///
    /// Replaces the scattered static calls to
    /// <c>KeePassLib.Utility.MessageService.ShowInfo/ShowWarning/ShowFatal/AskYesNo</c>
    /// with an injectable interface that can be mocked in tests and implemented
    /// by any UI framework (WinForms, Avalonia, console, etc.).
    ///
    /// Fan-in: <c>MessageService.ShowWarning</c> had 83 direct call sites;
    /// migrating those progressively to this interface is planned per EPIC-03.
    ///
    /// The methods accept a <c>message</c> string rather than a <c>params object[]</c>
    /// to keep the interface clean; callers must format the string before calling.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Displays an informational message.
        /// The dialog has a single OK button.
        /// </summary>
        /// <param name="message">Text to display.</param>
        /// <param name="title">
        /// Dialog title. When <c>null</c> the implementation uses its default
        /// application title (e.g. "KeePass").
        /// </param>
        void ShowInfo(string message, string title = null);

        /// <summary>
        /// Displays a warning message.
        /// The dialog has a single OK button and a warning icon.
        /// </summary>
        void ShowWarning(string message, string title = null);

        /// <summary>
        /// Displays an error message.
        /// The dialog has a single OK button and an error icon.
        /// </summary>
        void ShowError(string message, string title = null);

        /// <summary>
        /// Displays a fatal-error message and attempts to copy details to
        /// the clipboard so the user can report them.
        /// </summary>
        void ShowFatal(string message, string title = null);

        /// <summary>
        /// Asks the user a Yes/No question and returns <c>true</c> if Yes
        /// was selected.
        /// </summary>
        /// <param name="question">Question text.</param>
        /// <param name="title">Dialog title. <c>null</c> uses the default.</param>
        /// <param name="defaultToYes">
        /// When <c>true</c> (default), the Yes button is the default choice.
        /// </param>
        bool AskYesNo(string question, string title = null, bool defaultToYes = true);
    }
}
