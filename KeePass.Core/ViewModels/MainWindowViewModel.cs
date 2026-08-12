/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using KeePassLib;
using KeePassLib.Collections;

using KeePass.Core.Platform;
using KeePass.Core.Projections;
using KeePass.Core.Services;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for the main KeePass window.
	///
	/// <para>
	/// Wraps <see cref="IDatabaseSessionService"/> and exposes observable state
	/// (database list, group tree, entry list, selected-entry details) plus
	/// relay commands (open, close, save, lock/unlock).  Has zero references to
	/// <c>System.Windows.Forms</c> or <c>System.Drawing</c> and can be
	/// instantiated and tested independently of any WinForms Form.
	/// </para>
	/// </summary>
	public sealed class MainWindowViewModel : ObservableObject
	{
		private readonly IDatabaseSessionService _sessionService;
		private readonly IProjectionMapper<PwEntry, EntryProjection> _entryMapper;
		private readonly IProjectionMapper<PwGroup, GroupProjection> _groupMapper;
		private readonly IMessenger _messenger;
		private readonly IClipboardService _clipboardService;
		private readonly IGeneratorProfileStore _profileStore;

		// ── Observable backing fields ─────────────────────────────────────────

		private ObservableCollection<DatabaseSummaryDto> _databases
			= new ObservableCollection<DatabaseSummaryDto>();

		private int _activeDatabaseIndex = -1;

		private ObservableCollection<GroupProjection> _groupTree
			= new ObservableCollection<GroupProjection>();

		private GroupProjection _selectedGroup;

		private ObservableCollection<EntryProjection> _entryList
			= new ObservableCollection<EntryProjection>();

		private ObservableCollection<EntryProjection> _selectedEntries
			= new ObservableCollection<EntryProjection>();

		private bool _isLocked;
		private string _statusText = string.Empty;
		private string _quickSearchText = string.Empty;
		private CancellationTokenSource? _quickSearchCts;
		private bool _isPasswordRevealed;

		// ── Construction ─────────────────────────────────────────────────────

		/// <summary>
		/// Creates a <see cref="MainWindowViewModel"/>.
		/// </summary>
		/// <param name="sessionService">Database lifecycle service (required).</param>
		/// <param name="entryMapper">Maps PwEntry → EntryProjection (required).</param>
		/// <param name="groupMapper">Maps PwGroup → GroupProjection (required).</param>
		/// <param name="messenger">
		/// Messenger for cross-ViewModel notifications.
		/// Defaults to <see cref="WeakReferenceMessenger.Default"/> when <c>null</c>.
		/// </param>
		public MainWindowViewModel(
			IDatabaseSessionService sessionService,
			IProjectionMapper<PwEntry, EntryProjection> entryMapper,
			IProjectionMapper<PwGroup, GroupProjection> groupMapper,
			IMessenger messenger = null,
			IClipboardService clipboardService = null,
			IGeneratorProfileStore profileStore = null)
		{
			if(sessionService == null) throw new ArgumentNullException("sessionService");
			if(entryMapper == null)    throw new ArgumentNullException("entryMapper");
			if(groupMapper == null)    throw new ArgumentNullException("groupMapper");

			_sessionService   = sessionService;
			_entryMapper      = entryMapper;
			_groupMapper      = groupMapper;
			_messenger        = messenger ?? WeakReferenceMessenger.Default;
			_clipboardService = clipboardService;
			_profileStore     = profileStore;

			// Subscribe to service events
			_sessionService.SessionChanged += OnSessionChanged;

			// Commands
			NewDatabaseCommand     = new RelayCommand(NewDatabase);
			OpenDatabaseCommand    = new RelayCommand(OpenDatabase);
			CloseDatabaseCommand   = new RelayCommand(CloseDatabase,   () => IsDatabaseOpen);
			SaveDatabaseCommand    = new RelayCommand(SaveDatabase,    () => IsDatabaseOpen);
			LockWorkspaceCommand   = new RelayCommand(LockWorkspace,   () => EnableLockCmd);
			UnlockWorkspaceCommand = new RelayCommand(UnlockWorkspace, () => IsLocked && IsDatabaseOpen);

			// Entry CRUD commands
			AddEntryCommand        = new RelayCommand(AddEntry,    () => IsDatabaseOpen);
			EditEntryCommand       = new RelayCommand(EditEntry,   () => SelectedEntries.Count == 1);
			DeleteEntryCommand     = new RelayCommand(DeleteEntry, () => SelectedEntries.Count >= 1);

			// Clipboard commands
			CopyUserNameCommand = new RelayCommand(CopyUserName, () => CanCopyUserName);
			CopyPasswordCommand = new RelayCommand(CopyPassword, () => CanCopyPassword);
			CopyUrlCommand      = new RelayCommand(CopyUrl,      () => CanOpenUrl);

			// Preview panel commands
			TogglePasswordRevealCommand = new RelayCommand(
				() => IsPasswordRevealed = !IsPasswordRevealed);

			// Password generator command
			ShowPasswordGeneratorCommand = new RelayCommand(ShowPasswordGenerator);

			// Import/Export commands
			ImportCommand = new RelayCommand(() => ImportRequested?.Invoke(this, EventArgs.Empty), () => IsDatabaseOpen);
			ExportCommand = new RelayCommand(() => ExportRequested?.Invoke(this, EventArgs.Empty), () => IsDatabaseOpen);
			ExitCommand   = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));

			// Populate initial state
			RefreshAll();
		}

		// ── Observable properties ─────────────────────────────────────────────

		/// <summary>Summary descriptors for all open document slots.</summary>
		public ObservableCollection<DatabaseSummaryDto> Databases
		{
			get { return _databases; }
			private set { SetProperty(ref _databases, value); }
		}

		/// <summary>Zero-based index of the active database, or -1 when none.</summary>
		public int ActiveDatabaseIndex
		{
			get { return _activeDatabaseIndex; }
			set
			{
				if(SetProperty(ref _activeDatabaseIndex, value))
				{
					_sessionService.SetActiveDocument(value);
				}
			}
		}

		/// <summary>Flat depth-first list of all groups in the active database.</summary>
		public ObservableCollection<GroupProjection> GroupTree
		{
			get { return _groupTree; }
			private set { SetProperty(ref _groupTree, value); }
		}

		/// <summary>The currently selected group, or <c>null</c> when none is selected.</summary>
		public GroupProjection SelectedGroup
		{
			get { return _selectedGroup; }
			set
			{
				if(SetProperty(ref _selectedGroup, value))
				{
					RefreshEntryList();
					_messenger.Send(new GroupSelectedMessage(value));
					OnPropertyChanged("CanCopyUserName");
					OnPropertyChanged("CanCopyPassword");
					OnPropertyChanged("CanOpenUrl");
					OnPropertyChanged("CanPerformAutoType");
				}
			}
		}

		/// <summary>Entries in the currently selected group.</summary>
		public ObservableCollection<EntryProjection> EntryList
		{
			get { return _entryList; }
			private set { SetProperty(ref _entryList, value); }
		}

		/// <summary>The set of currently selected entries (may be empty or multi-selection).</summary>
		public ObservableCollection<EntryProjection> SelectedEntries
		{
			get { return _selectedEntries; }
			set
			{
				if(SetProperty(ref _selectedEntries, value))
				{
					OnPropertyChanged("CanCopyUserName");
					OnPropertyChanged("CanCopyPassword");
					OnPropertyChanged("CanOpenUrl");
					OnPropertyChanged("CanPerformAutoType");

					CloseDatabaseCommand.NotifyCanExecuteChanged();
					SaveDatabaseCommand.NotifyCanExecuteChanged();
					LockWorkspaceCommand.NotifyCanExecuteChanged();
					UnlockWorkspaceCommand.NotifyCanExecuteChanged();
					EditEntryCommand.NotifyCanExecuteChanged();
					DeleteEntryCommand.NotifyCanExecuteChanged();
					CopyUserNameCommand.NotifyCanExecuteChanged();
					CopyPasswordCommand.NotifyCanExecuteChanged();

					_isPasswordRevealed = false;
					OnPropertyChanged("IsPasswordRevealed");
					OnPropertyChanged("SelectedEntryPreview");
					OnPropertyChanged("HasSelectedEntry");
					OnPropertyChanged("PreviewPasswordText");

					EntryProjection primary = SelectedEntries.Count == 1
						? SelectedEntries[0] : null;
					_messenger.Send(new EntrySelectedMessage(primary));
				}
			}
		}

		/// <summary>Whether the active database is locked.</summary>
		public bool IsLocked
		{
			get { return _isLocked; }
			private set
			{
				if(SetProperty(ref _isLocked, value))
				{
					OnPropertyChanged("IsDatabaseOpen");
					OnPropertyChanged("EnableLockCmd");
					LockWorkspaceCommand.NotifyCanExecuteChanged();
					UnlockWorkspaceCommand.NotifyCanExecuteChanged();
				}
			}
		}

		/// <summary>Status bar text shown at the bottom of the window.</summary>
		public string StatusText
		{
			get { return _statusText; }
			set { SetProperty(ref _statusText, value); }
		}

		/// <summary>
		/// Quick-find text bound to the toolbar search box.
		/// Setting this property triggers a debounced search (300 ms delay).
		/// Clearing returns the entry list to the selected-group view.
		/// </summary>
		public string QuickSearchText
		{
			get { return _quickSearchText; }
			set
			{
				if(SetProperty(ref _quickSearchText, value ?? string.Empty))
					ScheduleQuickSearch();
			}
		}

		// ── Derived / computed properties ─────────────────────────────────────

		/// <summary>Whether any database with open content is currently active.</summary>
		public bool IsDatabaseOpen
		{
			get
			{
				PwDatabase db = _sessionService.GetActiveDatabase();
				return db != null && db.IsOpen && !IsLocked;
			}
		}

		/// <summary>Whether a single entry is selected and has a non-empty user-name.</summary>
		public bool CanCopyUserName
		{
			get
			{
				if(SelectedEntries.Count != 1) return false;
				string u = SelectedEntries[0].UserName != null
					? SelectedEntries[0].UserName.ReadString() : null;
				return !string.IsNullOrEmpty(u);
			}
		}

		/// <summary>Whether a single entry is selected (always has a password field).</summary>
		public bool CanCopyPassword
		{
			get { return SelectedEntries.Count == 1; }
		}

		/// <summary>Whether a single entry is selected and has a non-empty URL.</summary>
		public bool CanOpenUrl
		{
			get
			{
				if(SelectedEntries.Count != 1) return false;
				string u = SelectedEntries[0].Url != null
					? SelectedEntries[0].Url.ReadString() : null;
				return !string.IsNullOrEmpty(u);
			}
		}

		/// <summary>Whether auto-type is available for the single selected entry.</summary>
		public bool CanPerformAutoType
		{
			get
			{
				if(SelectedEntries.Count != 1) return false;
				return SelectedEntries[0].AutoTypeEnabled;
			}
		}

		/// <summary>Whether the lock-workspace command is available.</summary>
		public bool EnableLockCmd
		{
			get { return IsDatabaseOpen; }
		}

		// ── Preview panel properties ─────────────────────────────────────────

		/// <summary>The single selected entry for the preview panel, or null.</summary>
		public EntryProjection SelectedEntryPreview
		{
			get { return _selectedEntries.Count == 1 ? _selectedEntries[0] : null; }
		}

		/// <summary>Whether exactly one entry is selected (controls preview panel visibility).</summary>
		public bool HasSelectedEntry
		{
			get { return _selectedEntries.Count == 1; }
		}

		/// <summary>Whether the preview panel password is shown in plain text.</summary>
		public bool IsPasswordRevealed
		{
			get { return _isPasswordRevealed; }
			set
			{
				if(SetProperty(ref _isPasswordRevealed, value))
					OnPropertyChanged("PreviewPasswordText");
			}
		}

		/// <summary>Password text for the preview panel (masked or plain).</summary>
		public string PreviewPasswordText
		{
			get
			{
				EntryProjection ep = SelectedEntryPreview;
				if(ep == null || ep.Password == null) return string.Empty;
				if(_isPasswordRevealed)
					return ep.Password.ReadString();
				string pw = ep.Password.ReadString();
				if(string.IsNullOrEmpty(pw)) return string.Empty;
				return new string('\u2022', pw.Length);
			}
		}

		// ── Commands ─────────────────────────────────────────────────────────

		public IRelayCommand NewDatabaseCommand     { get; }
		public IRelayCommand OpenDatabaseCommand    { get; }
		public IRelayCommand CloseDatabaseCommand   { get; }
		public IRelayCommand SaveDatabaseCommand    { get; }
		public IRelayCommand LockWorkspaceCommand   { get; }
		public IRelayCommand UnlockWorkspaceCommand { get; }

		public IRelayCommand AddEntryCommand        { get; }
		public IRelayCommand EditEntryCommand       { get; }
		public IRelayCommand DeleteEntryCommand     { get; }
		public IRelayCommand CopyUserNameCommand    { get; }
		public IRelayCommand CopyPasswordCommand    { get; }
		public IRelayCommand CopyUrlCommand         { get; }
		public IRelayCommand TogglePasswordRevealCommand { get; }
		public IRelayCommand ShowPasswordGeneratorCommand { get; }
		public IRelayCommand ImportCommand  { get; }
		public IRelayCommand ExportCommand  { get; }
		public IRelayCommand ExitCommand    { get; }

		/// <summary>
		/// Raised when the application should shut down.
		/// </summary>
		public event EventHandler ExitRequested;

		/// <summary>
		/// Raised when the View should open an entry editor dialog.
		/// </summary>
		public event EventHandler<EntryEditorRequestEventArgs> EntryEditorRequested;

		/// <summary>
		/// Raised when the View should open a password generator dialog.
		/// </summary>
		public event EventHandler PasswordGeneratorRequested;

		/// <summary>
		/// Raised when the View should open the import dialog.
		/// </summary>
		public event EventHandler ImportRequested;

		/// <summary>
		/// Raised when the View should open the export dialog.
		/// </summary>
		public event EventHandler ExportRequested;

		private void NewDatabase()     { _sessionService.CreateNew(); }
		private void OpenDatabase()    { _sessionService.OpenDatabase(); }
		private void CloseDatabase()   { _sessionService.CloseDatabase(); }
		private void SaveDatabase()    { _sessionService.SaveDatabase(); }
		private void LockWorkspace()   { _sessionService.LockWorkspace(); }
		private void UnlockWorkspace() { _sessionService.UnlockWorkspace(); }

		private void AddEntry()
		{
			PwDatabase db = _sessionService.GetActiveDatabase();
			if(db == null || !db.IsOpen) return;

			var vm = new EntryEditorViewModel(null, _entryMapper as EntryProjectionMapper ?? new EntryProjectionMapper());
			EntryEditorRequested?.Invoke(this, new EntryEditorRequestEventArgs(vm, db, ResolveSelectedGroup(db)));
		}

		private void EditEntry()
		{
			if(SelectedEntries.Count != 1) return;
			PwDatabase db = _sessionService.GetActiveDatabase();
			if(db == null || !db.IsOpen) return;

			PwEntry entry = FindEntry(db, SelectedEntries[0].Uuid);
			if(entry == null) return;

			var vm = new EntryEditorViewModel(entry, _entryMapper as EntryProjectionMapper ?? new EntryProjectionMapper());
			EntryEditorRequested?.Invoke(this, new EntryEditorRequestEventArgs(vm, db, null));
		}

		private void DeleteEntry()
		{
			PwDatabase db = _sessionService.GetActiveDatabase();
			if(db == null || !db.IsOpen) return;

			foreach(EntryProjection ep in SelectedEntries.ToArray())
			{
				PwEntry entry = FindEntry(db, ep.Uuid);
				if(entry == null) continue;

				PwGroup parent = entry.ParentGroup;
				if(parent == null) continue;

				PwGroup recycleBin = db.RootGroup.FindGroup(db.RecycleBinUuid, true);
				if(recycleBin != null && recycleBin.Uuid != parent.Uuid)
				{
					parent.Entries.Remove(entry);
					recycleBin.Entries.Add(entry);
					entry.Touch(false);
				}
				else
				{
					parent.Entries.Remove(entry);
				}
			}

			db.Modified = true;
			RefreshEntryList();
		}

		private void CopyUserName()
		{
			if(_clipboardService == null || !_clipboardService.IsSupported) return;
			if(SelectedEntries.Count != 1) return;
			string user = SelectedEntries[0].UserName?.ReadString();
			if(!string.IsNullOrEmpty(user))
			{
				_clipboardService.SetWithAutoClear(user, TimeSpan.FromSeconds(12));
				StatusText = "User name copied — auto-clear in 12 s";
			}
		}

		private void CopyPassword()
		{
			if(_clipboardService == null || !_clipboardService.IsSupported) return;
			if(SelectedEntries.Count != 1) return;
			string pw = SelectedEntries[0].Password?.ReadString();
			if(pw != null)
			{
				_clipboardService.SetWithAutoClear(pw, TimeSpan.FromSeconds(12));
				StatusText = "Password copied — auto-clear in 12 s";
			}
		}

		private void CopyUrl()
		{
			if(_clipboardService == null || !_clipboardService.IsSupported) return;
			if(SelectedEntries.Count != 1) return;
			string url = SelectedEntries[0].Url?.ReadString();
			if(!string.IsNullOrEmpty(url))
			{
				_clipboardService.SetText(url);
				StatusText = "URL copied to clipboard";
			}
		}

		private void ShowPasswordGenerator()
		{
			PasswordGeneratorRequested?.Invoke(this, EventArgs.Empty);
		}

		private PwGroup ResolveSelectedGroup(PwDatabase db)
		{
			if(_selectedGroup != null)
				return db.RootGroup.FindGroup(_selectedGroup.Uuid, true) ?? db.RootGroup;
			return db.RootGroup;
		}

		private static PwEntry FindEntry(PwDatabase db, PwUuid uuid)
		{
			if(uuid == null || uuid.Equals(PwUuid.Zero)) return null;
			return db.RootGroup.FindEntry(uuid, true);
		}

		// ── Refresh helpers ───────────────────────────────────────────────────

		private void ScheduleQuickSearch()
		{
			// Cancel any pending search before scheduling a new one.
			_quickSearchCts?.Cancel();
			_quickSearchCts = new CancellationTokenSource();
			CancellationToken token = _quickSearchCts.Token;

			string text = _quickSearchText;

			if(string.IsNullOrWhiteSpace(text))
			{
				// Empty search — restore the normal group-filtered view.
				RefreshEntryList();
				return;
			}

			// Debounce: fire the actual search after 300 ms.
			Task.Run(async () =>
			{
				try
				{
					await Task.Delay(300, token);
					if(token.IsCancellationRequested) return;
					ExecuteQuickSearch(text, token);
				}
				catch(OperationCanceledException) { }
			}, token);
		}

		private void ExecuteQuickSearch(string text, CancellationToken token)
		{
			PwDatabase db = _sessionService.GetActiveDatabase();
			if(db == null || !db.IsOpen) return;

			var sp = new KeePassLib.SearchParameters
			{
				SearchString     = text,
				SearchInTitles   = true,
				SearchInUserNames = true,
				SearchInUrls     = true,
				SearchInNotes    = true,
				SearchInTags     = true,
			};

			var matches = new PwObjectList<PwEntry>();
			db.RootGroup.SearchEntries(sp, matches);

			if(token.IsCancellationRequested) return;

			// Marshal back to the collection — no dispatcher abstraction yet,
			// collection is modified on the calling thread.
			_entryList.Clear();
			_selectedEntries.Clear();
			for(uint i = 0; i < matches.UCount; i++)
				_entryList.Add(_entryMapper.FromDomain(matches.GetAt(i)));
		}

		private void OnSessionChanged(object sender, EventArgs e)
		{
			RefreshAll();
		}

		private void RefreshAll()
		{
			RefreshDatabases();
			RefreshGroupTree();
			RefreshEntryList();
			IsLocked = _sessionService.IsActiveDatabaseLocked;

			OnPropertyChanged("IsDatabaseOpen");
			OnPropertyChanged("EnableLockCmd");
			CloseDatabaseCommand.NotifyCanExecuteChanged();
			SaveDatabaseCommand.NotifyCanExecuteChanged();
			LockWorkspaceCommand.NotifyCanExecuteChanged();
			UnlockWorkspaceCommand.NotifyCanExecuteChanged();
			AddEntryCommand.NotifyCanExecuteChanged();
			EditEntryCommand.NotifyCanExecuteChanged();
			DeleteEntryCommand.NotifyCanExecuteChanged();
			CopyUserNameCommand.NotifyCanExecuteChanged();
			CopyPasswordCommand.NotifyCanExecuteChanged();
			ImportCommand.NotifyCanExecuteChanged();
			ExportCommand.NotifyCanExecuteChanged();

			PwDatabase db = _sessionService.GetActiveDatabase();
			_messenger.Send(new DatabaseChangedMessage(db));
		}

		private void RefreshDatabases()
		{
			IReadOnlyList<DatabaseSummaryDto> docs = _sessionService.GetDocuments();
			int newIndex = _sessionService.ActiveDocumentIndex;

			_databases.Clear();
			foreach(DatabaseSummaryDto doc in docs)
				_databases.Add(doc);

			// Suppress the set-active-document side-effect when syncing
			SetProperty(ref _activeDatabaseIndex, newIndex, "ActiveDatabaseIndex");
		}

		/// <summary>
		/// Walks the active database's group tree depth-first and populates
		/// <see cref="GroupTree"/> with a flat ordered list of group projections.
		/// </summary>
		private void RefreshGroupTree()
		{
			_groupTree.Clear();
			SelectedGroup = null;

			PwDatabase db = _sessionService.GetActiveDatabase();
			if(db == null || !db.IsOpen) return;

			WalkGroups(db.RootGroup);
		}

		private void WalkGroups(PwGroup group)
		{
			if(group == null) return;

			GroupProjection proj = _groupMapper.FromDomain(group);
			_groupTree.Add(proj);

			for(uint i = 0; i < group.Groups.UCount; i++)
				WalkGroups(group.Groups.GetAt(i));
		}

		/// <summary>
		/// Maps entries from the selected group (direct children only) into
		/// <see cref="EntryList"/>.  Uses the root group when nothing is selected.
		/// </summary>
		private void RefreshEntryList()
		{
			_entryList.Clear();
			_selectedEntries.Clear();

			PwDatabase db = _sessionService.GetActiveDatabase();
			if(db == null || !db.IsOpen) return;

			// Resolve the actual PwGroup from the projection UUID
			PwGroup source = _selectedGroup != null
				? db.RootGroup.FindGroup(_selectedGroup.Uuid, true)
				: db.RootGroup;

			if(source == null) return;

			for(uint i = 0; i < source.Entries.UCount; i++)
			{
				PwEntry entry = source.Entries.GetAt(i);
				_entryList.Add(_entryMapper.FromDomain(entry));
			}
		}
	}

	/// <summary>
	/// Event args for <see cref="MainWindowViewModel.EntryEditorRequested"/>.
	/// </summary>
	public sealed class EntryEditorRequestEventArgs : EventArgs
	{
		public EntryEditorViewModel ViewModel { get; }
		public PwDatabase Database { get; }
		public PwGroup TargetGroup { get; }

		public EntryEditorRequestEventArgs(EntryEditorViewModel vm, PwDatabase db, PwGroup targetGroup)
		{
			ViewModel   = vm;
			Database    = db;
			TargetGroup = targetGroup;
		}
	}
}
