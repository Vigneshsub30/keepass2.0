using System;
using System.Collections.Generic;
using System.Diagnostics;

using KeePass.App;
using KeePass.App.Configuration;
using KeePass.DataExchange;
using KeePass.Native;
using KeePass.UI;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;
using KeePassLib.Utility;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeePass.Services
{
    /// <summary>
    /// Encapsulates vault lifecycle operations: open, close, lock, unlock,
    /// save, and synchronize.  MainForm becomes a thin subscriber to the
    /// events raised here, delegating all document-tracking and lock-timer
    /// state to this coordinator.
    ///
    /// <para>Constructor injection: <see cref="DocumentManagerEx"/>,
    /// <see cref="IOptions{T}"/> for <see cref="AppConfigEx"/>,
    /// <see cref="Core.Services.IMessageService"/>, and
    /// <see cref="ILogger{T}"/>.</para>
    ///
    /// <para>UI-blocking operations (progress dialogs, shutdown blockers) are
    /// driven by the caller (MainForm) via callbacks passed to individual
    /// lifecycle methods; the coordinator owns only the pure domain logic.</para>
    /// </summary>
    public sealed class DatabaseSessionCoordinator
    {
        // ── Dependencies ──────────────────────────────────────────────────────

        private readonly DocumentManagerEx m_docMgr;
        private readonly IOptions<AppConfigEx> m_config;
        private readonly Core.Services.IMessageService m_msg;
        private readonly ILogger<DatabaseSessionCoordinator> m_log;

        // ── Lock timer state (moved from MainForm) ────────────────────────────

        private readonly CriticalSectionEx m_csLockTimer = new CriticalSectionEx();

        /// <summary>Maximum inactivity seconds before workspace lock (0 = disabled).</summary>
        private int m_nLockTimerMax;

        /// <summary>UTC ticks at which the inactivity lock fires.</summary>
        private long m_lLockAtTicks = long.MaxValue;

        /// <summary>Last known global input timestamp (OS-level).</summary>
        private uint m_uLastInputTime = uint.MaxValue;

        /// <summary>UTC ticks at which the global inactivity lock fires.</summary>
        private long m_lLockAtGlobalTicks = long.MaxValue;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised after a database has been opened successfully.
        /// Subscribers update the UI (tab bar, entry list, etc.).
        /// </summary>
        public event EventHandler<DatabaseOpenedEventArgs> DatabaseOpened;

        /// <summary>
        /// Raised after a database has been closed.
        /// Subscribers update the UI and MRU list.
        /// </summary>
        public event EventHandler<DatabaseClosedEventArgs> DatabaseClosed;

        /// <summary>
        /// Raised after all open databases have been locked.
        /// Subscribers update the UI to show the locked state.
        /// </summary>
        public event EventHandler<WorkspaceLockedEventArgs> WorkspaceLocked;

        /// <summary>
        /// Raised after a workspace unlock (i.e. a database was re-opened
        /// from a locked state).  Mirrors <see cref="DatabaseOpened"/>.
        /// </summary>
        public event EventHandler<WorkspaceUnlockedEventArgs> WorkspaceUnlocked;

        /// <summary>
        /// Raised after a save operation completes (successfully or not).
        /// Subscribers refresh the modified-flag indicator in the UI.
        /// </summary>
        public event EventHandler<DatabaseSavedEventArgs> DatabaseSaved;

        // ── Ctor ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the coordinator.
        /// </summary>
        /// <param name="docMgr">
        /// The application-wide document manager.  Owned by <c>MainForm</c>;
        /// the coordinator holds a reference but does not own the lifetime.
        /// </param>
        /// <param name="config">Application configuration snapshot.</param>
        /// <param name="messageService">
        /// Platform-neutral message service for unsaved-changes prompts.
        /// </param>
        /// <param name="logger">Structured logger.</param>
        public DatabaseSessionCoordinator(
            DocumentManagerEx docMgr,
            IOptions<AppConfigEx> config,
            Core.Services.IMessageService messageService,
            ILogger<DatabaseSessionCoordinator> logger)
        {
            if(docMgr == null) throw new ArgumentNullException("docMgr");
            if(config == null) throw new ArgumentNullException("config");
            if(messageService == null) throw new ArgumentNullException("messageService");
            if(logger == null) throw new ArgumentNullException("logger");

            m_docMgr = docMgr;
            m_config = config;
            m_msg = messageService;
            m_log = logger;

            m_nLockTimerMax = (int)config.Value.Security.WorkspaceLocking.LockAfterTime;
        }

        // ── Lock timer ────────────────────────────────────────────────────────

        /// <summary>
        /// Resets the inactivity lock timer.  Must be called whenever the
        /// user performs an action that counts as "activity" (keyboard, mouse).
        /// </summary>
        public void NotifyUserActivity()
        {
            if(m_nLockTimerMax == 0)
                m_lLockAtTicks = long.MaxValue;
            else
            {
                m_lLockAtTicks = DateTime.UtcNow
                    .AddSeconds((double)m_nLockTimerMax).Ticks;
            }

            Program.TriggerSystem.NotifyUserActivity();
        }

        /// <summary>
        /// Reloads the lock timeout from the current configuration.
        /// Call after the user changes security settings.
        /// </summary>
        public void RefreshLockTimerMax()
        {
            m_nLockTimerMax = (int)m_config.Value.Security.WorkspaceLocking.LockAfterTime;
        }

        /// <summary>
        /// Checks whether a timer-driven lock should fire right now.
        /// Call from the main timer tick (roughly once per second).
        /// </summary>
        /// <param name="utcNow">Current UTC time.</param>
        /// <param name="lockAction">
        /// Action invoked when the timer fires.  Typically <c>LockAllDocuments</c>.
        /// The action runs on the caller's thread.
        /// </param>
        public void EvaluateLockTimers(DateTime utcNow,
            Action lockAction)
        {
            if(lockAction == null) return;

            UpdateGlobalLockTimeout(utcNow);

            long lNow = utcNow.Ticks;
            if((lNow >= m_lLockAtTicks) || (lNow >= m_lLockAtGlobalTicks))
            {
                m_lLockAtTicks = long.MaxValue;
                m_lLockAtGlobalTicks = long.MaxValue;
                lockAction();
            }
        }

        // ── Save ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Saves <paramref name="pd"/> to its current
        /// <see cref="PwDatabase.IOConnectionInfo"/>.
        ///
        /// <para>The caller is responsible for UI-blocking (progress dialogs,
        /// shutdown blockers) around this call.  Pass a logger that the caller
        /// has already started; the coordinator does not start or stop it.</para>
        /// </summary>
        /// <param name="pd">The database to save.</param>
        /// <param name="logger">
        /// Status logger (may be <see cref="NullStatusLogger.Instance"/>).
        /// </param>
        /// <returns>
        /// <c>true</c> if saved successfully; <c>false</c> if the save was
        /// aborted or an exception occurred.
        /// </returns>
		public bool SaveDatabase(PwDatabase pd, IStatusLogger logger)
		{
			if(pd == null) throw new ArgumentNullException("pd");
			if(logger == null) logger = new NullStatusLogger();

            if(!pd.IsOpen)
            {
                m_log.LogWarning("SaveDatabase called on a closed database; ignoring.");
                return false;
            }

            ApplySaveConfiguration(pd);

            bool bSuccess = true;
            try
            {
                pd.Save(logger);

                m_log.LogInformation(
                    "Database saved: {Path}", pd.IOConnectionInfo.GetDisplayName());
            }
            catch(Exception ex)
            {
                bSuccess = false;
                m_log.LogError(ex, "Database save failed: {Path}",
                    pd.IOConnectionInfo.GetDisplayName());
            }

            DatabaseSaved?.Invoke(this,
                new DatabaseSavedEventArgs(pd, pd.IOConnectionInfo.CloneDeep(), bSuccess));

            return bSuccess;
        }

        // ── Close ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Closes a single database document.
        ///
        /// <para>If the database has unsaved changes the caller-supplied
        /// <paramref name="askSaveDelegate"/> is invoked to decide whether to
        /// save, discard, or cancel.  Returning <c>null</c> cancels the close.</para>
        /// </summary>
        /// <param name="dsToClose">
        /// Document to close; <c>null</c> closes the currently active document.
        /// </param>
        /// <param name="flags">Reason for closing (locking, exiting, etc.).</param>
        /// <param name="logger">Status logger for any auto-save that occurs.</param>
        /// <param name="askSaveDelegate">
        /// Function called when the database is modified and auto-save is
        /// disabled.  Should return <c>true</c> to save, <c>false</c> to
        /// discard changes, or <c>null</c> to cancel the entire close.
        /// When <c>null</c>, unsaved changes are silently discarded.
        /// </param>
        /// <returns>
        /// <c>true</c> if the database was closed; <c>false</c> if the user
        /// cancelled.
        /// </returns>
		public bool CloseDatabase(PwDocument dsToClose, DatabaseCloseFlags flags,
            IStatusLogger logger, Func<PwDatabase, DatabaseCloseFlags, bool?> askSaveDelegate)
		{
			PwDocument ds = (dsToClose ?? m_docMgr.ActiveDocument);
			PwDatabase pd = ds.Database;

			if(logger == null) logger = new NullStatusLogger();

            bool bLocking = (flags & DatabaseCloseFlags.Locking) != 0;
            bool bExiting = (flags & DatabaseCloseFlags.Exiting) != 0;

            if(pd.Modified) // Implies pd.IsOpen
            {
                bool bCanAutoSave = AppPolicy.Current.SaveFile;
                bool bSave;

                if(m_config.Value.Application.FileClosing.AutoSave && bCanAutoSave)
                {
                    bSave = true;
                }
                else if(askSaveDelegate != null)
                {
                    bool? choice = askSaveDelegate(pd, flags);
                    if(choice == null) return false; // Cancel
                    bSave = choice.Value;
                }
                else
                {
                    bSave = false; // Silently discard
                }

                if(bSave)
                {
                    ApplySaveConfiguration(pd);
                    bool saved = SaveDatabase(pd, logger);
                    if(!saved || pd.Modified)
                    {
                        m_log.LogWarning(
                            "Auto-save before close failed; aborting close for {Path}",
                            pd.IOConnectionInfo.GetDisplayName());
                        return false;
                    }
                }
            }

            IOConnectionInfo ioClosing = pd.IOConnectionInfo.CloneDeep();
            pd.Close();

            if(!bLocking)
                m_docMgr.CloseDatabase(pd);

            m_log.LogInformation(
                "Database closed ({Flags}): {Path}", flags,
                ioClosing.GetDisplayName());

            DatabaseClosed?.Invoke(this,
                new DatabaseClosedEventArgs(pd, ioClosing, flags));

            return true;
        }

        // ── Lock / Unlock ─────────────────────────────────────────────────────

        /// <summary>
        /// Locks all open databases.  Each database is closed via
        /// <see cref="CloseDatabase"/> with <see cref="DatabaseCloseFlags.Locking"/>,
        /// and the locked IOC is stored on the document for later unlock.
        ///
        /// <para>If
        /// <see cref="AceWorkspaceLocking.AlwaysExitInsteadOfLocking"/> is set,
        /// <paramref name="exitAction"/> is invoked instead.</para>
        /// </summary>
        /// <param name="logger">Status logger for any auto-saves triggered by close.</param>
        /// <param name="askSaveDelegate">Passed through to <see cref="CloseDatabase"/>.</param>
        /// <param name="exitAction">
        /// Action invoked when the policy requires exit-instead-of-lock.
        /// </param>
        /// <returns>
        /// Number of documents that were locked (0 if exit action was invoked
        /// or if nothing was open).
        /// </returns>
        public int LockAllDocuments(
            IStatusLogger logger,
            Func<PwDatabase, DatabaseCloseFlags, bool?> askSaveDelegate,
            Action exitAction)
        {
            NotifyUserActivity();

            if(m_config.Value.Security.WorkspaceLocking.AlwaysExitInsteadOfLocking)
            {
                exitAction?.Invoke();
                return 0;
            }

            List<PwDocument> lDocs = m_docMgr.GetDocuments(int.MaxValue);
            int nLocked = 0;
            foreach(PwDocument ds in lDocs)
            {
                PwDatabase pd = ds.Database;
                if(!pd.IsOpen) continue;

                IOConnectionInfo ioIoc = pd.IOConnectionInfo;
                Debug.Assert(ioIoc != null);

                bool closed = CloseDatabase(ds, DatabaseCloseFlags.Locking,
                    logger, askSaveDelegate);
                if(!closed || pd.IsOpen) continue;

                ds.LockedIoc = ioIoc;
                ++nLocked;
            }

            m_log.LogInformation("Workspace locked; {Count} document(s) locked.", nLocked);

            WorkspaceLocked?.Invoke(this, new WorkspaceLockedEventArgs(nLocked));

            return nLocked;
        }

        /// <summary>
        /// Raises <see cref="WorkspaceUnlocked"/> after a previously locked
        /// database has been re-opened.  Called by <c>MainForm</c> after
        /// <c>OpenDatabase</c> successfully reopens a locked document.
        /// </summary>
        public void NotifyUnlocked(PwDatabase pd, IOConnectionInfo ioc)
        {
            if(pd == null) throw new ArgumentNullException("pd");

            m_log.LogInformation("Workspace unlocked: {Path}",
                (ioc ?? pd.IOConnectionInfo).GetDisplayName());

            WorkspaceUnlocked?.Invoke(this,
                new WorkspaceUnlockedEventArgs(pd, ioc ?? pd.IOConnectionInfo.CloneDeep()));
        }

        /// <summary>
        /// Raises <see cref="DatabaseOpened"/> for a database that has been
        /// opened by <c>MainForm.OpenDatabase</c>.  The coordinator does not
        /// drive the open flow because it involves WinForms dialogs (key prompt,
        /// file picker); the flow remains in <c>MainForm</c>.
        /// </summary>
        public void NotifyOpened(PwDatabase pd)
        {
            if(pd == null) throw new ArgumentNullException("pd");

            m_log.LogInformation("Database opened: {Path}",
                pd.IOConnectionInfo.GetDisplayName());

            DatabaseOpened?.Invoke(this,
                new DatabaseOpenedEventArgs(pd, pd.IOConnectionInfo.CloneDeep()));
        }

        // ── Synchronize ───────────────────────────────────────────────────────

        /// <summary>
        /// Merges changes from <paramref name="iocSource"/> into
        /// <paramref name="pd"/> and saves the result.
        ///
        /// <para>Uses <see cref="ImportUtil.Synchronize"/> for the merge;
        /// the caller drives the progress UI.</para>
        /// </summary>
        /// <param name="pd">Database to synchronize.</param>
        /// <param name="iocSource">Source file to merge from.</param>
        /// <param name="logger">Status logger.</param>
		/// <param name="uiOps">
        /// UI operations handler required by <c>ImportUtil.Synchronize</c>.
        /// Typically the <c>MainForm</c> instance.
        /// </param>
        /// <param name="fParent">
        /// Parent WinForms window (may be same object as <paramref name="uiOps"/>
        /// when <c>MainForm</c> implements both <c>IUIOperations</c> and <c>Form</c>).
        /// </param>
        /// <returns>
        /// The result of <c>ImportUtil.Synchronize</c>: <c>true</c> on
        /// success, <c>false</c> on failure, <c>null</c> if not supported.
        /// </returns>
        public bool? SynchronizeDatabase(PwDatabase pd, IOConnectionInfo iocSource,
            IStatusLogger logger, KeePassLib.Interfaces.IUIOperations uiOps,
            System.Windows.Forms.Form fParent)
        {
            if(pd == null) throw new ArgumentNullException("pd");
            if(iocSource == null) throw new ArgumentNullException("iocSource");
            if(logger == null) logger = new NullStatusLogger();

            bool? result = ImportUtil.Synchronize(pd, uiOps, iocSource, false, fParent);

            m_log.LogInformation(
                "Synchronize result={Result} for {Path}",
                result, pd.IOConnectionInfo.GetDisplayName());

            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Applies configuration-driven save options (transactions, file locks)
        /// to <paramref name="pd"/> before each save.
        /// </summary>
        private void ApplySaveConfiguration(PwDatabase pd)
        {
            pd.UseFileTransactions = m_config.Value.Application.UseTransactedFileWrites;
            pd.UseFileLocks = m_config.Value.Application.UseFileLocks;
        }

        /// <summary>
        /// Updates <see cref="m_lLockAtGlobalTicks"/> based on the OS-reported
        /// last-input time.  Called by <see cref="EvaluateLockTimers"/>.
        /// </summary>
        private void UpdateGlobalLockTimeout(DateTime utcNow)
        {
            uint uLockGlobal = m_config.Value.Security.WorkspaceLocking.LockAfterGlobalTime;
            if(uLockGlobal == 0) { m_lLockAtGlobalTicks = long.MaxValue; return; }

            uint? uLastInputTime = NativeMethods.GetLastInputTime();
            if(!uLastInputTime.HasValue) return;

            if(uLastInputTime.Value != m_uLastInputTime)
            {
                m_lLockAtGlobalTicks = utcNow.AddSeconds((double)uLockGlobal).Ticks;
                m_uLastInputTime = uLastInputTime.Value;
            }
        }
    }
}
