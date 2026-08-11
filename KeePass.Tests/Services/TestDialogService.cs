using System.Collections.Generic;

using KeePass.Core.Services;

namespace KeePass.Tests.Services
{
    /// <summary>
    /// Recording stub of <see cref="IDialogService"/> for use in unit tests.
    ///
    /// All show-dialog methods return configurable values (defaulting to
    /// <c>null</c> / <c>-1</c>) and record their calls for assertion.
    /// </summary>
    public sealed class TestDialogService : IDialogService
    {
        /// <summary>Represents one recorded call to a dialog method.</summary>
        public sealed class Call
        {
            public string Method { get; }
            public object[] Args { get; }

            internal Call(string method, params object[] args)
            {
                Method = method;
                Args   = args;
            }
        }

        private readonly List<Call> _calls = new List<Call>();

        /// <summary>All calls recorded since this instance was created.</summary>
        public IReadOnlyList<Call> Calls => _calls;

        /// <summary>Clears all recorded calls.</summary>
        public void Reset() => _calls.Clear();

        // ── Configurable return values ────────────────────────────────────

        /// <summary>Value returned by <see cref="ShowOpenFileDialog"/>. Default: <c>null</c>.</summary>
        public string OpenFileDialogResult { get; set; }

        /// <summary>Value returned by <see cref="ShowSaveFileDialog"/>. Default: <c>null</c>.</summary>
        public string SaveFileDialogResult { get; set; }

        /// <summary>Value returned by <see cref="ShowInputDialog"/>. Default: <c>null</c>.</summary>
        public string InputDialogResult { get; set; }

        /// <summary>Value returned by <see cref="ShowTaskDialog"/>. Default: <c>-1</c> (cancelled).</summary>
        public int TaskDialogResult { get; set; } = -1;

        // ── IDialogService ────────────────────────────────────────────────

        public string ShowOpenFileDialog(string title, string filter = null,
            string initialDirectory = null)
        {
            _calls.Add(new Call(nameof(ShowOpenFileDialog), title, filter, initialDirectory));
            return OpenFileDialogResult;
        }

        public string ShowSaveFileDialog(string title, string filter = null,
            string initialDirectory = null, string defaultFileName = null)
        {
            _calls.Add(new Call(nameof(ShowSaveFileDialog), title, filter,
                initialDirectory, defaultFileName));
            return SaveFileDialogResult;
        }

        public string ShowInputDialog(string prompt, string title = null,
            string initialValue = null)
        {
            _calls.Add(new Call(nameof(ShowInputDialog), prompt, title, initialValue));
            return InputDialogResult;
        }

        public int ShowTaskDialog(TaskDialogModel model)
        {
            _calls.Add(new Call(nameof(ShowTaskDialog), model));
            if (model != null && model.VerificationText != null)
                model.VerificationResult = false;
            return TaskDialogResult;
        }
    }
}
