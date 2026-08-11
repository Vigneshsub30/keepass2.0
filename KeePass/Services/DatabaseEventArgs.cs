using System;

using KeePassLib;
using KeePassLib.Serialization;

namespace KeePass.Services
{
    /// <summary>
    /// Base event args carrying the database and its connection info
    /// for all vault lifecycle events raised by
    /// <see cref="DatabaseSessionCoordinator"/>.
    /// </summary>
    public class DatabaseEventArgs : EventArgs
    {
        /// <summary>The database that was acted on. Never <c>null</c>.</summary>
        public PwDatabase Database { get; }

        /// <summary>
        /// Connection info at the time of the event.  May differ from
        /// <c>Database.IOConnectionInfo</c> for close/lock events where the
        /// IOConnectionInfo has already been cleared.
        /// </summary>
        public IOConnectionInfo ConnectionInfo { get; }

        /// <summary>
        /// <c>true</c> if the operation completed successfully;
        /// <c>false</c> if it was aborted or encountered an error.
        /// </summary>
        public bool Success { get; }

        /// <summary>Initialises a new instance of <see cref="DatabaseEventArgs"/>.</summary>
        public DatabaseEventArgs(PwDatabase database, IOConnectionInfo connectionInfo,
            bool success = true)
        {
            if(database == null) throw new ArgumentNullException("database");
            Database = database;
            ConnectionInfo = connectionInfo ?? new IOConnectionInfo();
            Success = success;
        }
    }

    /// <summary>
    /// Event args for <see cref="DatabaseSessionCoordinator.DatabaseOpened"/>.
    /// </summary>
    public sealed class DatabaseOpenedEventArgs : DatabaseEventArgs
    {
        /// <summary>
        /// Creates a new <see cref="DatabaseOpenedEventArgs"/>.
        /// </summary>
        public DatabaseOpenedEventArgs(PwDatabase database, IOConnectionInfo connectionInfo)
            : base(database, connectionInfo, true) { }
    }

    /// <summary>
    /// Event args for <see cref="DatabaseSessionCoordinator.DatabaseClosed"/>.
    /// </summary>
    public sealed class DatabaseClosedEventArgs : DatabaseEventArgs
    {
        /// <summary>Flags that describe why the database was closed.</summary>
        public DatabaseCloseFlags Flags { get; }

        /// <summary>
        /// Creates a new <see cref="DatabaseClosedEventArgs"/>.
        /// </summary>
        public DatabaseClosedEventArgs(PwDatabase database,
            IOConnectionInfo connectionInfo, DatabaseCloseFlags flags)
            : base(database, connectionInfo, true)
        {
            Flags = flags;
        }
    }

    /// <summary>
    /// Event args for <see cref="DatabaseSessionCoordinator.DatabaseSaved"/>.
    /// </summary>
    public sealed class DatabaseSavedEventArgs : DatabaseEventArgs
    {
        /// <summary>
        /// Creates a new <see cref="DatabaseSavedEventArgs"/>.
        /// </summary>
        public DatabaseSavedEventArgs(PwDatabase database,
            IOConnectionInfo connectionInfo, bool success)
            : base(database, connectionInfo, success) { }
    }

    /// <summary>
    /// Event args for <see cref="DatabaseSessionCoordinator.WorkspaceLocked"/>.
    /// </summary>
    public sealed class WorkspaceLockedEventArgs : EventArgs
    {
        /// <summary>Number of documents that were locked.</summary>
        public int DocumentCount { get; }

        /// <summary>Initialises a new instance.</summary>
        public WorkspaceLockedEventArgs(int documentCount)
        {
            DocumentCount = documentCount;
        }
    }

    /// <summary>
    /// Event args for <see cref="DatabaseSessionCoordinator.WorkspaceUnlocked"/>.
    /// </summary>
    public sealed class WorkspaceUnlockedEventArgs : DatabaseEventArgs
    {
        /// <summary>Initialises a new instance.</summary>
        public WorkspaceUnlockedEventArgs(PwDatabase database,
            IOConnectionInfo connectionInfo)
            : base(database, connectionInfo, true) { }
    }

    /// <summary>
    /// Flags indicating why a database was closed, matching the original
    /// <c>FileEventFlags</c> semantics in <c>MainForm</c>.
    /// </summary>
    [Flags]
    public enum DatabaseCloseFlags
    {
        /// <summary>Normal user-initiated close.</summary>
        None = 0,

        /// <summary>The database was closed as part of a workspace lock.</summary>
        Locking = 1,

        /// <summary>The database was closed because the application is exiting.</summary>
        Exiting = 2,

        /// <summary>The close was triggered by the ECAS trigger system.</summary>
        Ecas = 4
    }
}
