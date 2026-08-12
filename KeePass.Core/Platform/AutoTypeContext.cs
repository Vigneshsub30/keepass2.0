namespace KeePass.Core.Platform
{
    /// <summary>
    /// Platform-neutral data transfer object carrying the state needed to
    /// perform an auto-type sequence for a single entry.
    /// </summary>
    public sealed class AutoTypeContext
    {
        /// <summary>The auto-type sequence to send (e.g., "{USERNAME}{TAB}{PASSWORD}{ENTER}").</summary>
        public string Sequence { get; }

        /// <summary>Target window title hint (used for matching on Windows).</summary>
        public string TargetWindowTitle { get; }

        /// <summary>
        /// Initializes a new <see cref="AutoTypeContext"/>.
        /// </summary>
        /// <param name="sequence">Auto-type key sequence. Must not be null or empty.</param>
        /// <param name="targetWindowTitle">Optional target window title hint; may be null.</param>
        public AutoTypeContext(string sequence, string targetWindowTitle)
        {
            if (string.IsNullOrEmpty(sequence))
                throw new System.ArgumentNullException("sequence");
            Sequence = sequence;
            TargetWindowTitle = targetWindowTitle;
        }
    }
}
