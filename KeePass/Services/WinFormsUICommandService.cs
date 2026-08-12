using System;
using System.Diagnostics;

using KeePass.Core.Services;
using KeePass.Forms;
using KeePass.UI;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace KeePass.Services
{
    /// <summary>
    /// WinForms implementation of <see cref="IUICommandService"/>.
    ///
    /// <para>Delegates every call to the <see cref="MainForm"/> instance
    /// obtained from <see cref="Program.MainForm"/>.  The coordinator stays
    /// null-safe: if <c>Program.MainForm</c> is not yet available the call
    /// is silently dropped with a debug assertion.</para>
    /// </summary>
    public sealed class WinFormsUICommandService : IUICommandService
    {
        // ── IUICommandService ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public void OpenDatabase(IOConnectionInfo ioc, CompositeKey compositeKey,
            bool openLocal)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.OpenDatabase(ioc, compositeKey, openLocal);
        }

        /// <inheritdoc/>
        public void SaveActiveDatabase()
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.UIFileSave(false);
        }

        /// <inheritdoc/>
        public void CloseActiveDatabase(bool ecas)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.CloseDocument(null, false, false, ecas, true);
        }

        /// <inheritdoc/>
        public PwDatabase GetActiveDatabase()
        {
            MainForm mf = Program.MainForm;
            if(mf == null) return null;
            return mf.ActiveDatabase;
        }

        /// <inheritdoc/>
        public object GetDocumentManager()
        {
            MainForm mf = Program.MainForm;
            if(mf == null) return null;
            return mf.DocumentManager;
        }

        /// <inheritdoc/>
        public void MakeDocumentActive(object doc)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            PwDocument pwDoc = doc as PwDocument;
            if(pwDoc == null) { Debug.Assert(false); return; }
            mf.MakeDocumentActive(pwDoc);
        }

        /// <inheritdoc/>
        public PwEntry GetSelectedEntry(bool withContext)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) return null;
            return mf.GetSelectedEntry(withContext);
        }

        /// <inheritdoc/>
        public void ShowEntriesByTag(string tag)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.ShowEntriesByTag(tag, false);
        }

        /// <inheritdoc/>
        public void AddCustomToolBarButton(string id, string name, string description)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.AddCustomToolBarButton(id, name, description);
        }

        /// <inheritdoc/>
        public void RemoveCustomToolBarButton(string id)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.RemoveCustomToolBarButton(id);
        }

        /// <inheritdoc/>
        public void SetInteractionBlocked(bool blocked)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.UIBlockInteraction(blocked);
        }

        /// <inheritdoc/>
        public IOConnectionInfo CompleteConnectionInfoUsingMru(IOConnectionInfo ioc)
        {
            MainForm mf = Program.MainForm;
            if(mf == null) return ioc;
            return mf.CompleteConnectionInfoUsingMru(ioc);
        }

        /// <inheritdoc/>
        public void ExecuteGlobalAutoType()
        {
            MainForm mf = Program.MainForm;
            if(mf == null) { Debug.Assert(false); return; }
            mf.ExecuteGlobalAutoType();
        }
    }
}
