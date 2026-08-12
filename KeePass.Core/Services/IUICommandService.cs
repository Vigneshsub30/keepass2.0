using KeePassLib;
using KeePassLib.Serialization;

namespace KeePass.Core.Services
{
    /// <summary>
    /// Platform-neutral abstraction for UI-level commands that the Ecas trigger
    /// system (and other application-layer services) need to invoke on the main
    /// window without taking a hard dependency on
    /// <c>System.Windows.Forms.Form</c> or <c>Program.MainForm</c>.
    ///
    /// <para>Implemented by <c>WinFormsUICommandService</c> in the WinForms head
    /// and by test stubs in the test project.  Future Avalonia head will provide
    /// its own implementation.</para>
    ///
    /// <para>This interface replaces the 15+ direct <c>Program.MainForm.*</c>
    /// calls inside <c>EcasDefaultActionProvider</c> (WO-037).</para>
    /// </summary>
    public interface IUICommandService
    {
        // ── Database lifecycle ────────────────────────────────────────────────

        /// <summary>
        /// Opens the database at <paramref name="ioc"/> with
        /// <paramref name="compositeKey"/> (or prompts for a key if <c>null</c>).
        /// </summary>
        void OpenDatabase(IOConnectionInfo ioc, KeePassLib.Keys.CompositeKey compositeKey,
            bool openLocal);

        /// <summary>Saves the currently active database to its current path.</summary>
        void SaveActiveDatabase();

        /// <summary>
        /// Closes the active database document.
        /// <paramref name="ecas"/> indicates the close was triggered by the ECAS
        /// system (used for plugin event routing).
        /// </summary>
        void CloseActiveDatabase(bool ecas);

        // ── Document management ───────────────────────────────────────────────

        /// <summary>
        /// Returns the currently active (selected) <see cref="PwDatabase"/>,
        /// or <c>null</c> if no database is open.
        /// </summary>
        PwDatabase GetActiveDatabase();

        /// <summary>
        /// Returns the <see cref="DocumentManagerEx"/> managing the open
        /// documents.
        /// </summary>
        object GetDocumentManager(); // Returns DocumentManagerEx; typed as object to avoid WinForms dep.

        /// <summary>
        /// Makes the document containing <paramref name="doc"/> the active tab.
        /// </summary>
        /// <param name="doc">Opaque document reference (a <c>PwDocument</c>).</param>
        void MakeDocumentActive(object doc);

        // ── Entry operations ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the currently selected <see cref="PwEntry"/>, or <c>null</c>
        /// if none is selected.
        /// </summary>
        /// <param name="withContext">
        /// When <c>true</c>, the call may involve a focus-dependent lookup.
        /// </param>
        PwEntry GetSelectedEntry(bool withContext);

        // ── Display operations ────────────────────────────────────────────────

        /// <summary>
        /// Filters the entry list to show only entries that carry
        /// <paramref name="tag"/> and scrolls the list into view.
        /// </summary>
        void ShowEntriesByTag(string tag);

        /// <summary>
        /// Adds a custom button to the main toolbar.
        /// </summary>
        void AddCustomToolBarButton(string id, string name, string description);

        /// <summary>
        /// Removes a previously added custom toolbar button.
        /// </summary>
        void RemoveCustomToolBarButton(string id);

        // ── Interaction blocking ──────────────────────────────────────────────

        /// <summary>
        /// Blocks or unblocks user interaction (equivalent to
        /// <c>MainForm.UIBlockInteraction</c>).
        /// </summary>
        void SetInteractionBlocked(bool blocked);

        // ── MRU connection info ───────────────────────────────────────────────

        /// <summary>
        /// Completes <paramref name="ioc"/> using entries from the most-recently-
        /// used connection list (credentials, save mode, etc.).
        /// </summary>
        IOConnectionInfo CompleteConnectionInfoUsingMru(IOConnectionInfo ioc);

        // ── Auto-type ─────────────────────────────────────────────────────────

        /// <summary>Triggers the global auto-type sequence.</summary>
        void ExecuteGlobalAutoType();
    }
}
