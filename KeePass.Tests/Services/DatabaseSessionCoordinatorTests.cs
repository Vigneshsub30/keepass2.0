using System;
using System.Collections.Generic;

using KeePass.App.Configuration;
using KeePass.Services;
using KeePass.Tests.Services;
using KeePass.UI;

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace KeePass.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="DatabaseSessionCoordinator"/>.
    ///
    /// <para>These tests verify lifecycle orchestration — event firing, delegate
    /// dispatch, and lock-timer logic — without requiring a WinForms message pump
    /// or a real KDBX file on disk.  A <see cref="DocumentManagerEx"/> with an
    /// in-memory <see cref="PwDatabase"/> is used as the backing store.</para>
    /// </summary>
    public sealed class DatabaseSessionCoordinatorTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static DatabaseSessionCoordinator CreateCoordinator(
            DocumentManagerEx docMgr = null,
            TestMessageService msg = null,
            AppConfigEx config = null)
        {
            docMgr = docMgr ?? new DocumentManagerEx();
            msg    = msg    ?? new TestMessageService();
            config = config ?? new AppConfigEx();

            IOptions<AppConfigEx> opts = new AppConfigExOptions(config);
            return new DatabaseSessionCoordinator(
                docMgr,
                opts,
                msg,
                NullLogger<DatabaseSessionCoordinator>.Instance);
        }

        // ── Constructor ───────────────────────────────────────────────────────

        [Fact]
        public void Constructor_NullDocMgr_Throws()
        {
            AppConfigEx config = new AppConfigEx();
            Assert.Throws<ArgumentNullException>(() =>
                new DatabaseSessionCoordinator(
                    null,
                    new AppConfigExOptions(config),
                    new TestMessageService(),
                    NullLogger<DatabaseSessionCoordinator>.Instance));
        }

        [Fact]
        public void Constructor_NullOptions_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DatabaseSessionCoordinator(
                    new DocumentManagerEx(),
                    null,
                    new TestMessageService(),
                    NullLogger<DatabaseSessionCoordinator>.Instance));
        }

        [Fact]
        public void Constructor_NullMessageService_Throws()
        {
            AppConfigEx config = new AppConfigEx();
            Assert.Throws<ArgumentNullException>(() =>
                new DatabaseSessionCoordinator(
                    new DocumentManagerEx(),
                    new AppConfigExOptions(config),
                    null,
                    NullLogger<DatabaseSessionCoordinator>.Instance));
        }

        [Fact]
        public void Constructor_NullLogger_Throws()
        {
            AppConfigEx config = new AppConfigEx();
            Assert.Throws<ArgumentNullException>(() =>
                new DatabaseSessionCoordinator(
                    new DocumentManagerEx(),
                    new AppConfigExOptions(config),
                    new TestMessageService(),
                    null));
        }

        // ── NotifyUserActivity ────────────────────────────────────────────────

        [Fact]
        public void NotifyUserActivity_WithZeroTimeout_DoesNotThrow()
        {
            AppConfigEx config = new AppConfigEx();
            config.Security.WorkspaceLocking.LockAfterTime = 0;

            DatabaseSessionCoordinator coord = CreateCoordinator(config: config);
            coord.NotifyUserActivity(); // Must not throw.
        }

        [Fact]
        public void NotifyUserActivity_WithPositiveTimeout_DoesNotThrow()
        {
            AppConfigEx config = new AppConfigEx();
            config.Security.WorkspaceLocking.LockAfterTime = 300; // 5 min

            DatabaseSessionCoordinator coord = CreateCoordinator(config: config);
            coord.NotifyUserActivity(); // Must not throw.
        }

        // ── EvaluateLockTimers ────────────────────────────────────────────────

        [Fact]
        public void EvaluateLockTimers_NullAction_DoesNotThrow()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            coord.EvaluateLockTimers(DateTime.UtcNow, null); // Must not throw.
        }

        [Fact]
        public void EvaluateLockTimers_TimerNotExpired_DoesNotInvokeLockAction()
        {
            AppConfigEx config = new AppConfigEx();
            config.Security.WorkspaceLocking.LockAfterTime = 600; // 10 min
            config.Security.WorkspaceLocking.LockAfterGlobalTime = 0;

            DatabaseSessionCoordinator coord = CreateCoordinator(config: config);
            coord.NotifyUserActivity(); // Set timer far in the future.

            bool lockFired = false;
            coord.EvaluateLockTimers(DateTime.UtcNow, () => lockFired = true);

            Assert.False(lockFired, "Lock must not fire before the timeout expires.");
        }

        // ── NotifyOpened event ────────────────────────────────────────────────

        [Fact]
        public void NotifyOpened_RaisesDatabaseOpenedEvent()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            DatabaseOpenedEventArgs received = null;
            coord.DatabaseOpened += (s, e) => received = e;

            PwDatabase pd = new PwDatabase();
            pd.IOConnectionInfo.Path = "/tmp/test.kdbx";

            coord.NotifyOpened(pd);

            Assert.NotNull(received);
            Assert.Same(pd, received.Database);
        }

        [Fact]
        public void NotifyOpened_NullDatabase_Throws()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            Assert.Throws<ArgumentNullException>(() => coord.NotifyOpened(null));
        }

        // ── NotifyUnlocked event ──────────────────────────────────────────────

        [Fact]
        public void NotifyUnlocked_RaisesWorkspaceUnlockedEvent()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            WorkspaceUnlockedEventArgs received = null;
            coord.WorkspaceUnlocked += (s, e) => received = e;

            PwDatabase pd = new PwDatabase();
            pd.IOConnectionInfo.Path = "/tmp/test.kdbx";

            coord.NotifyUnlocked(pd, pd.IOConnectionInfo.CloneDeep());

            Assert.NotNull(received);
            Assert.Same(pd, received.Database);
        }

        [Fact]
        public void NotifyUnlocked_NullDatabase_Throws()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            Assert.Throws<ArgumentNullException>(() =>
                coord.NotifyUnlocked(null, new IOConnectionInfo()));
        }

        // ── SaveDatabase event ────────────────────────────────────────────────

        [Fact]
        public void SaveDatabase_ClosedDatabase_ReturnsFalse_NoEvent()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            DatabaseSavedEventArgs received = null;
            coord.DatabaseSaved += (s, e) => received = e;

            PwDatabase pd = new PwDatabase(); // Not open

            bool result = coord.SaveDatabase(pd, new NullStatusLogger());

            Assert.False(result, "Saving a closed database must return false.");
            Assert.Null(received);
        }

        [Fact]
        public void SaveDatabase_NullDatabase_Throws()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            Assert.Throws<ArgumentNullException>(() =>
                coord.SaveDatabase(null, new NullStatusLogger()));
        }

        [Fact]
        public void SaveDatabase_NullLogger_UsesNullLogger()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            PwDatabase pd = new PwDatabase(); // Not open

            // Must not throw even with null logger.
            bool result = coord.SaveDatabase(pd, null);
            Assert.False(result);
        }

        // ── CloseDatabase ─────────────────────────────────────────────────────

        [Fact]
        public void CloseDatabase_NullDatabase_Throws()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            Assert.Throws<NullReferenceException>(() =>
                coord.CloseDatabase(null, DatabaseCloseFlags.None,
                    new NullStatusLogger(), null));
        }

        [Fact]
        public void CloseDatabase_UnmodifiedDatabase_ClosesWithoutPrompt()
        {
            DocumentManagerEx docMgr = new DocumentManagerEx();
            DatabaseSessionCoordinator coord = CreateCoordinator(docMgr);

            DatabaseClosedEventArgs closed = null;
            coord.DatabaseClosed += (s, e) => closed = e;

            bool askInvoked = false;
            bool result = coord.CloseDatabase(
                docMgr.ActiveDocument,
                DatabaseCloseFlags.None,
                new NullStatusLogger(),
                (db, flags) => { askInvoked = true; return true; });

            Assert.True(result, "Close should succeed for an unmodified database.");
            Assert.False(askInvoked, "Prompt must not be shown for an unmodified database.");
            Assert.NotNull(closed);
        }

        [Fact]
        public void CloseDatabase_CancelFromDelegate_ReturnsFalse()
        {
            DocumentManagerEx docMgr = new DocumentManagerEx();
            DatabaseSessionCoordinator coord = CreateCoordinator(docMgr);

            // Mark as modified so the delegate is called.
            PwDatabase pd = docMgr.ActiveDatabase;
            pd.Modified = true;

            // Disable auto-save so the delegate is queried.
            AppConfigEx config = new AppConfigEx();
            config.Application.FileClosing.AutoSave = false;
            IOptions<AppConfigEx> opts = new AppConfigExOptions(config);

            TestMessageService msg = new TestMessageService();
            DatabaseSessionCoordinator coordNoAutoSave = new DatabaseSessionCoordinator(
                docMgr, opts, msg, NullLogger<DatabaseSessionCoordinator>.Instance);

            DatabaseClosedEventArgs closed = null;
            coordNoAutoSave.DatabaseClosed += (s, e) => closed = e;

            // Delegate returns null → cancel.
            bool result = coordNoAutoSave.CloseDatabase(
                docMgr.ActiveDocument,
                DatabaseCloseFlags.None,
                new NullStatusLogger(),
                (db, flags) => null);

            Assert.False(result, "Close must return false when the user cancels.");
            Assert.Null(closed); // DatabaseClosed must not fire when close is cancelled.
        }

        [Fact]
        public void CloseDatabase_DiscardFromDelegate_ClosesWithoutSave()
        {
            DocumentManagerEx docMgr = new DocumentManagerEx();

            AppConfigEx config = new AppConfigEx();
            config.Application.FileClosing.AutoSave = false;
            IOptions<AppConfigEx> opts = new AppConfigExOptions(config);

            TestMessageService msg = new TestMessageService();
            DatabaseSessionCoordinator coord = new DatabaseSessionCoordinator(
                docMgr, opts, msg, NullLogger<DatabaseSessionCoordinator>.Instance);

            DatabaseClosedEventArgs closed = null;
            coord.DatabaseClosed += (s, e) => closed = e;

            // Mark as modified.
            docMgr.ActiveDatabase.Modified = true;

            // Delegate returns false → discard changes.
            bool result = coord.CloseDatabase(
                docMgr.ActiveDocument,
                DatabaseCloseFlags.None,
                new NullStatusLogger(),
                (db, flags) => false);

            Assert.True(result, "Close should succeed when changes are discarded.");
            Assert.NotNull(closed);
        }

        // ── LockAllDocuments ──────────────────────────────────────────────────

        [Fact]
        public void LockAllDocuments_ExitInsteadOfLocking_InvokesExitAction()
        {
            AppConfigEx config = new AppConfigEx();
            config.Security.WorkspaceLocking.AlwaysExitInsteadOfLocking = true;

            DatabaseSessionCoordinator coord = CreateCoordinator(config: config);

            bool exitCalled = false;
            int locked = coord.LockAllDocuments(
                new NullStatusLogger(),
                (db, flags) => null,
                () => exitCalled = true);

            Assert.True(exitCalled, "Exit action must be invoked when policy mandates it.");
            Assert.Equal(0, locked);
        }

        [Fact]
        public void LockAllDocuments_NothingOpen_RaisesWorkspaceLocked_ZeroCount()
        {
            DatabaseSessionCoordinator coord = CreateCoordinator();
            WorkspaceLockedEventArgs received = null;
            coord.WorkspaceLocked += (s, e) => received = e;

            int locked = coord.LockAllDocuments(
                new NullStatusLogger(),
                (db, flags) => null,
                null);

            // WorkspaceLocked fires but count is 0 (default doc has no open db).
            Assert.NotNull(received);
            Assert.Equal(0, received.DocumentCount);
            Assert.Equal(0, locked);
        }

        // ── DatabaseCloseFlags enum ───────────────────────────────────────────

        [Fact]
        public void DatabaseCloseFlags_Locking_ValueIsOneFlag()
        {
            Assert.Equal(1, (int)DatabaseCloseFlags.Locking);
        }

        [Fact]
        public void DatabaseCloseFlags_Exiting_ValueIsDistinct()
        {
            Assert.NotEqual(DatabaseCloseFlags.Locking, DatabaseCloseFlags.Exiting);
        }

        [Fact]
        public void DatabaseCloseFlags_CanCombine()
        {
            DatabaseCloseFlags combined = DatabaseCloseFlags.Locking | DatabaseCloseFlags.Exiting;
            Assert.True((combined & DatabaseCloseFlags.Locking) != 0);
            Assert.True((combined & DatabaseCloseFlags.Exiting) != 0);
        }

        // ── DatabaseEventArgs ─────────────────────────────────────────────────

        [Fact]
        public void DatabaseEventArgs_NullDatabase_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DatabaseEventArgs(null, new IOConnectionInfo(), true));
        }

        [Fact]
        public void DatabaseEventArgs_NullConnectionInfo_UsesEmptyIoc()
        {
            PwDatabase pd = new PwDatabase();
            DatabaseEventArgs args = new DatabaseEventArgs(pd, null, true);
            Assert.NotNull(args.ConnectionInfo);
        }

        [Fact]
        public void DatabaseOpenedEventArgs_ReflectsInputs()
        {
            PwDatabase pd = new PwDatabase();
            IOConnectionInfo ioc = new IOConnectionInfo { Path = "/tmp/test.kdbx" };

            DatabaseOpenedEventArgs args = new DatabaseOpenedEventArgs(pd, ioc);

            Assert.Same(pd, args.Database);
            Assert.Equal("/tmp/test.kdbx", args.ConnectionInfo.Path);
            Assert.True(args.Success);
        }

        [Fact]
        public void WorkspaceLockedEventArgs_DocumentCount_IsSet()
        {
            WorkspaceLockedEventArgs args = new WorkspaceLockedEventArgs(3);
            Assert.Equal(3, args.DocumentCount);
        }
    }
}
