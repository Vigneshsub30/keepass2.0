using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using KeePass.Core.Services;
using KeePass.Core.ViewModels;
using KeePass.Desktop.Avalonia.Views;

using CoreFilter = KeePass.Core.Services.FileDialogFilter;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace KeePass.Desktop.Avalonia.Services
{
	internal sealed class AvaloniaSessionService : IDatabaseSessionService
	{
		private readonly IFileDialogService _fileDialog;
		private readonly Func<Window?> _windowFactory;
		private readonly IKeyFileLocator _keyFileLocator;
		private readonly List<DocumentSlot> _documents = new();
		private int _activeIndex = -1;

		private static readonly IReadOnlyList<CoreFilter> KdbxFilters = new[]
		{
			new CoreFilter { Name = "KeePass Database", Extensions = new[] { "kdbx" } },
			new CoreFilter { Name = "All Files", Extensions = new[] { "*" } }
		};

		public AvaloniaSessionService(
			IFileDialogService fileDialog,
			Func<Window?> windowFactory,
			IKeyFileLocator keyFileLocator)
		{
			_fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));
			_windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
			_keyFileLocator = keyFileLocator ?? throw new ArgumentNullException(nameof(keyFileLocator));
		}

		public event EventHandler? SessionChanged;

		public IReadOnlyList<DatabaseSummaryDto> GetDocuments()
		{
			return _documents.Select(d => new DatabaseSummaryDto
			{
				Name = string.IsNullOrEmpty(d.Database.IOConnectionInfo?.Path)
					? "(Untitled)"
					: System.IO.Path.GetFileName(d.Database.IOConnectionInfo.Path),
				Path = d.Database.IOConnectionInfo?.Path ?? string.Empty,
				IsModified = d.Database.Modified,
				IsLocked = d.IsLocked,
				IsOpen = d.Database.IsOpen
			}).ToList();
		}

		public int ActiveDocumentIndex => _activeIndex;

		public PwDatabase? GetActiveDatabase()
		{
			if (_activeIndex < 0 || _activeIndex >= _documents.Count)
				return null;
			var slot = _documents[_activeIndex];
			return slot.IsLocked ? null : slot.Database;
		}

		public PwDatabase? GetDatabase(int index)
		{
			if (index < 0 || index >= _documents.Count)
				return null;
			var slot = _documents[index];
			return slot.IsLocked ? null : slot.Database;
		}

		public bool IsActiveDatabaseLocked
		{
			get
			{
				if (_activeIndex < 0 || _activeIndex >= _documents.Count)
					return false;
				return _documents[_activeIndex].IsLocked;
			}
		}

		public void SetActiveDocument(int index)
		{
			if (index < 0 || index >= _documents.Count) return;
			_activeIndex = index;
			SessionChanged?.Invoke(this, EventArgs.Empty);
		}

		public async void OpenDatabase()
		{
			string? path = await _fileDialog.OpenFileAsync("Open KeePass Database", KdbxFilters);
			if (string.IsNullOrEmpty(path)) return;

			var existing = _documents.FindIndex(d =>
				string.Equals(d.Database.IOConnectionInfo?.Path, path,
					StringComparison.OrdinalIgnoreCase));
			if (existing >= 0)
			{
				_activeIndex = existing;
				SessionChanged?.Invoke(this, EventArgs.Empty);
				return;
			}

			var ioc = IOConnectionInfo.FromPath(path);
			await PromptAndOpen(ioc);
		}

		public async void CreateNew()
		{
			string? path = await _fileDialog.SaveFileAsync(
				"Create New KeePass Database", KdbxFilters, "NewDatabase.kdbx");
			if (string.IsNullOrEmpty(path)) return;

			var ioc = IOConnectionInfo.FromPath(path);

			Window? owner = _windowFactory();
			if (owner == null) return;

			var keyVm = new KeyPromptViewModel(ioc, _keyFileLocator, _fileDialog);
			var keyView = new KeyPromptView { DataContext = keyVm };
			var dialog = new Window
			{
				Title = "Set Master Key",
				Content = keyView,
				Width = 500,
				Height = 350,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				CanResize = false
			};

			var tcs = new TaskCompletionSource<CompositeKey?>();

			keyVm.UnlockSucceeded += (_, key) =>
			{
				tcs.TrySetResult(key);
				Dispatcher.UIThread.Post(() => dialog.Close());
			};
			keyVm.UnlockFailed += (_, msg) =>
			{
				tcs.TrySetResult(null);
				Dispatcher.UIThread.Post(() => dialog.Close());
			};
			keyView.Cancelled += (_, _) =>
			{
				tcs.TrySetResult(null);
				Dispatcher.UIThread.Post(() => dialog.Close());
			};

			await dialog.ShowDialog(owner);
			var compositeKey = tcs.Task.IsCompleted ? tcs.Task.Result : null;
			if (compositeKey == null) return;

			var db = new PwDatabase();
			db.New(ioc, compositeKey);

			var slot = new DocumentSlot { Database = db };
			_documents.Add(slot);
			_activeIndex = _documents.Count - 1;

			db.Save(null);
			SessionChanged?.Invoke(this, EventArgs.Empty);
		}

		public void CloseDatabase()
		{
			var db = GetActiveDatabase();
			if (db == null) return;

			if (db.Modified)
			{
				db.Save(null);
			}

			db.Close();
			_documents.RemoveAt(_activeIndex);
			_activeIndex = _documents.Count > 0 ? 0 : -1;
			SessionChanged?.Invoke(this, EventArgs.Empty);
		}

		public void SaveDatabase()
		{
			var db = GetActiveDatabase();
			if (db == null || !db.IsOpen) return;
			db.Save(null);
			SessionChanged?.Invoke(this, EventArgs.Empty);
		}

		public void LockWorkspace()
		{
			if (_activeIndex < 0 || _activeIndex >= _documents.Count) return;
			var slot = _documents[_activeIndex];
			if (!slot.Database.IsOpen || slot.IsLocked) return;

			slot.LockedIoc = slot.Database.IOConnectionInfo;
			if (slot.Database.Modified)
				slot.Database.Save(null);
			slot.Database.Close();
			SessionChanged?.Invoke(this, EventArgs.Empty);
		}

		public async void UnlockWorkspace()
		{
			if (_activeIndex < 0 || _activeIndex >= _documents.Count) return;
			var slot = _documents[_activeIndex];
			if (!slot.IsLocked || slot.LockedIoc == null) return;

			await PromptAndOpen(slot.LockedIoc, slot);
		}

		private async Task PromptAndOpen(IOConnectionInfo ioc, DocumentSlot? existingSlot = null)
		{
			Window? owner = _windowFactory();
			if (owner == null) return;

			var keyVm = new KeyPromptViewModel(ioc, _keyFileLocator, _fileDialog);
			var keyView = new KeyPromptView { DataContext = keyVm };
			var dialog = new Window
			{
				Title = $"Unlock — {System.IO.Path.GetFileName(ioc.Path)}",
				Content = keyView,
				Width = 500,
				Height = 350,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				CanResize = false
			};

			var tcs = new TaskCompletionSource<CompositeKey?>();

			keyVm.UnlockSucceeded += (_, key) =>
			{
				tcs.TrySetResult(key);
				Dispatcher.UIThread.Post(() => dialog.Close());
			};
			keyVm.UnlockFailed += (_, msg) =>
			{
				tcs.TrySetResult(null);
				Dispatcher.UIThread.Post(() => dialog.Close());
			};
			keyView.Cancelled += (_, _) =>
			{
				tcs.TrySetResult(null);
				Dispatcher.UIThread.Post(() => dialog.Close());
			};

			await dialog.ShowDialog(owner);
			var compositeKey = tcs.Task.IsCompleted ? tcs.Task.Result : null;
			if (compositeKey == null) return;

			try
			{
				if (existingSlot != null)
				{
					existingSlot.Database.Open(ioc, compositeKey, null);
					existingSlot.LockedIoc = null;
				}
				else
				{
					var db = new PwDatabase();
					db.Open(ioc, compositeKey, null);
					var slot = new DocumentSlot { Database = db };
					_documents.Add(slot);
					_activeIndex = _documents.Count - 1;
				}

				SessionChanged?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to open database: {ex.Message}");
			}
		}

		private sealed class DocumentSlot
		{
			public PwDatabase Database { get; set; } = new PwDatabase();
			public IOConnectionInfo? LockedIoc { get; set; }
			public bool IsLocked => LockedIoc != null;
		}
	}
}
