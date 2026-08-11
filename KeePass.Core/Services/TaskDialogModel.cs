namespace KeePass.Core.Services
{
    /// <summary>
    /// Platform-neutral data-transfer object that describes a task-dialog prompt.
    ///
    /// Covers the feature set of <c>KeePass.UI.VistaTaskDialog</c>:
    /// main instruction, body content, icon, buttons, default button, footer,
    /// optional verification checkbox, and command-link style.
    ///
    /// Instances are created by callers and passed to
    /// <see cref="IDialogService.ShowTaskDialog"/>.
    /// </summary>
    public sealed class TaskDialogModel
    {
        /// <summary>Bold main instruction shown at the top of the dialog.</summary>
        public string MainInstruction { get; set; }

        /// <summary>Secondary body text below the main instruction.</summary>
        public string Content { get; set; }

        /// <summary>
        /// Severity icon displayed next to the main instruction.
        /// Defaults to <see cref="MessageSeverity.Info"/> (information icon).
        /// </summary>
        public MessageSeverity Severity { get; set; } = MessageSeverity.Info;

        /// <summary>
        /// Buttons to show. Each element is a label string.
        /// A single-element array renders a simple OK dialog.
        /// Two-element arrays are typically Yes/No or OK/Cancel.
        /// </summary>
        public string[] Buttons { get; set; }

        /// <summary>
        /// Zero-based index into <see cref="Buttons"/> for the default button.
        /// Defaults to 0.
        /// </summary>
        public int DefaultButtonIndex { get; set; } = 0;

        /// <summary>
        /// Optional text shown in the footer area below the divider.
        /// <c>null</c> means no footer.
        /// </summary>
        public string FooterText { get; set; }

        /// <summary>
        /// Icon for the footer area.  Only relevant when
        /// <see cref="FooterText"/> is non-null.
        /// </summary>
        public MessageSeverity FooterSeverity { get; set; } = MessageSeverity.Info;

        /// <summary>
        /// Optional label for a verification checkbox shown at the bottom.
        /// <c>null</c> means no checkbox.
        /// </summary>
        public string VerificationText { get; set; }

        /// <summary>
        /// Whether the verification checkbox is initially checked.
        /// Only relevant when <see cref="VerificationText"/> is non-null.
        /// </summary>
        public bool VerificationChecked { get; set; }

        /// <summary>
        /// When <c>true</c>, buttons are rendered as command links
        /// (large, icon-bearing buttons). When <c>false</c>, standard push buttons.
        /// </summary>
        public bool UseCommandLinks { get; set; }

        /// <summary>
        /// After <see cref="IDialogService.ShowTaskDialog"/> returns,
        /// reflects whether the user checked the verification checkbox.
        /// Only meaningful when <see cref="VerificationText"/> is non-null.
        /// </summary>
        public bool VerificationResult { get; set; }
    }
}
