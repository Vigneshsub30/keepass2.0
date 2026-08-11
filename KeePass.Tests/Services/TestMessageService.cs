using System.Collections.Generic;

using KeePass.Core.Services;

namespace KeePass.Tests.Services
{
    /// <summary>
    /// Recording stub of <see cref="IMessageService"/> for use in unit tests.
    ///
    /// Records every call in <see cref="Calls"/> so tests can assert on the
    /// exact sequence of messages shown without any UI interaction.
    /// </summary>
    public sealed class TestMessageService : IMessageService
    {
        /// <summary>Represents one recorded call to a message method.</summary>
        public sealed class Call
        {
            public string Method  { get; }
            public string Message { get; }
            public string Title   { get; }
            public bool?  YesNoDefaultToYes { get; }

            internal Call(string method, string message, string title,
                bool? defaultToYes = null)
            {
                Method  = method;
                Message = message;
                Title   = title;
                YesNoDefaultToYes = defaultToYes;
            }
        }

        private readonly List<Call> _calls = new List<Call>();

        /// <summary>All calls recorded since this instance was created.</summary>
        public IReadOnlyList<Call> Calls => _calls;

        /// <summary>
        /// Clears all recorded calls.  Useful when one test instance is reused
        /// across multiple sub-tests.
        /// </summary>
        public void Reset() => _calls.Clear();

        /// <summary>
        /// Value returned by <see cref="AskYesNo"/>.  Defaults to <c>true</c>.
        /// Override per-test to simulate the user clicking No.
        /// </summary>
        public bool AskYesNoResult { get; set; } = true;

        // ── IMessageService ───────────────────────────────────────────────

        public void ShowInfo(string message, string title = null)
            => _calls.Add(new Call(nameof(ShowInfo), message, title));

        public void ShowWarning(string message, string title = null)
            => _calls.Add(new Call(nameof(ShowWarning), message, title));

        public void ShowError(string message, string title = null)
            => _calls.Add(new Call(nameof(ShowError), message, title));

        public void ShowFatal(string message, string title = null)
            => _calls.Add(new Call(nameof(ShowFatal), message, title));

        public bool AskYesNo(string question, string title = null, bool defaultToYes = true)
        {
            _calls.Add(new Call(nameof(AskYesNo), question, title, defaultToYes));
            return AskYesNoResult;
        }
    }
}
