using System;
using System.Collections.Generic;

using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using KeePassLib;

namespace KeePass.Desktop.Avalonia
{
	/// <summary>
	/// Placeholder <see cref="IDatabaseSessionService"/> that reports no open
	/// documents. Replace with a real implementation once the database I/O
	/// layer is wired in.
	/// </summary>
	internal sealed class NullDatabaseSessionService : IDatabaseSessionService
	{
		public event EventHandler? SessionChanged { add { } remove { } }

		public IReadOnlyList<DatabaseSummaryDto> GetDocuments() =>
			Array.Empty<DatabaseSummaryDto>();

		public int ActiveDocumentIndex => -1;

		public PwDatabase? GetActiveDatabase() => null;

		public PwDatabase? GetDatabase(int index) => null;

		public bool IsActiveDatabaseLocked => false;

		public void SetActiveDocument(int index) { }

		public void CreateNew() { }

		public void OpenDatabase() { }

		public void CloseDatabase() { }

		public void SaveDatabase() { }

		public void LockWorkspace() { }

		public void UnlockWorkspace() { }
	}
}
