using System;
using System.Collections.Generic;

using KeePass.Core.Projections;
using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using KeePassLib;
using KeePassLib.Keys;

using Microsoft.Extensions.DependencyInjection;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Helper that builds a test <see cref="IServiceProvider"/> pre-wired with
	/// mock implementations of all service interfaces. Use in headless and unit
	/// tests that need a fully-populated DI container without real platform
	/// services.
	/// </summary>
	public static class TestServiceProvider
	{
		/// <summary>
		/// Builds a DI container suitable for headless test runs.
		/// </summary>
		public static IServiceProvider Build()
		{
			var services = new ServiceCollection();

			services.AddSingleton<IDatabaseSessionService, StubDatabaseSessionService>();
			services.AddSingleton<EntryProjectionMapper>();
			services.AddSingleton<GroupProjectionMapper>();
			services.AddTransient<MainWindowViewModel>(sp =>
				new MainWindowViewModel(
					sp.GetRequiredService<IDatabaseSessionService>(),
					sp.GetRequiredService<EntryProjectionMapper>(),
					sp.GetRequiredService<GroupProjectionMapper>()));

			return services.BuildServiceProvider();
		}

		// ------------------------------------------------------------------ //
		// Stub implementations                                                 //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// In-memory stub that holds real <see cref="PwDatabase"/> instances so
		/// the view-model's group-tree and entry-list refresh paths are fully
		/// exercised without file I/O.
		/// </summary>
		public sealed class StubDatabaseSessionService : IDatabaseSessionService
		{
			private readonly List<PwDatabase> _dbs = new List<PwDatabase>();
			private int _activeIndex = -1;

			public event EventHandler? SessionChanged;

			public IReadOnlyList<DatabaseSummaryDto> GetDocuments()
			{
				var result = new List<DatabaseSummaryDto>();
				foreach (var db in _dbs)
				{
					result.Add(new DatabaseSummaryDto
					{
						Name = System.IO.Path.GetFileNameWithoutExtension(db.IOConnectionInfo.Path),
						Path = db.IOConnectionInfo.Path,
						IsOpen = db.IsOpen,
						IsModified = db.Modified,
						IsLocked = false
					});
				}
				return result;
			}

			public int ActiveDocumentIndex => _activeIndex;

			public PwDatabase? GetActiveDatabase() =>
				(_activeIndex >= 0 && _activeIndex < _dbs.Count)
					? _dbs[_activeIndex] : null;

			public PwDatabase? GetDatabase(int index) =>
				(index >= 0 && index < _dbs.Count) ? _dbs[index] : null;

			public bool IsActiveDatabaseLocked => false;

			public void SetActiveDocument(int index)
			{
				if (index >= -1 && index < _dbs.Count)
					_activeIndex = index;
			}

			public void CreateNew() => RaiseSessionChanged();

			public void OpenDatabase() => RaiseSessionChanged();

			public void CloseDatabase()
			{
				if (_activeIndex >= 0 && _activeIndex < _dbs.Count)
				{
					_dbs.RemoveAt(_activeIndex);
					_activeIndex = _dbs.Count > 0 ? 0 : -1;
				}
				RaiseSessionChanged();
			}

			public void SaveDatabase() { }

			public void LockWorkspace() => RaiseSessionChanged();

			public void UnlockWorkspace() => RaiseSessionChanged();

			/// <summary>
			/// Adds a <see cref="PwDatabase"/> to the session and makes it active.
			/// </summary>
			public void AddDatabase(PwDatabase db)
			{
				if (db == null) throw new ArgumentNullException(nameof(db));
				_dbs.Add(db);
				_activeIndex = _dbs.Count - 1;
				RaiseSessionChanged();
			}

			/// <summary>Removes a specific database from the session.</summary>
			public void CloseDatabase(PwDatabase db)
			{
				int idx = _dbs.IndexOf(db);
				if (idx < 0) return;
				_dbs.RemoveAt(idx);
				_activeIndex = _dbs.Count > 0 ? 0 : -1;
				RaiseSessionChanged();
			}

			public void RaiseSessionChanged() =>
				SessionChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
