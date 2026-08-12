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
using System.Collections.Specialized;
using System.ComponentModel;

using KeePassLib;
using KeePassLib.Security;
using KeePass.Core.Projections;
using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using CommunityToolkit.Mvvm.Messaging;

using Xunit;

namespace KeePass.Tests.Platform
{
	// ── Mock session service ──────────────────────────────────────────────────

	/// <summary>Controllable stub for <see cref="IDatabaseSessionService"/>.</summary>
	internal sealed class MockSessionService : IDatabaseSessionService
	{
		private readonly List<PwDatabase> _dbs = new List<PwDatabase>();
		private int _activeIndex = -1;
		private bool _isLocked;

		public List<string> Log { get; } = new List<string>();

		public event EventHandler SessionChanged;

		public void SetupDatabase(PwDatabase db, bool makeActive = true)
		{
			_dbs.Add(db);
			if(makeActive) _activeIndex = _dbs.Count - 1;
		}

		public void SimulateSessionChanged()
		{
			SessionChanged?.Invoke(this, EventArgs.Empty);
		}

		public void SetLocked(bool locked) { _isLocked = locked; }

		// ── IDatabaseSessionService ───────────────────────────────────────────

		public IReadOnlyList<DatabaseSummaryDto> GetDocuments()
		{
			var list = new List<DatabaseSummaryDto>();
			foreach(PwDatabase db in _dbs)
			{
				list.Add(new DatabaseSummaryDto
				{
					Name       = db.Name,
					Path       = db.IOConnectionInfo?.Path ?? string.Empty,
					IsOpen     = db.IsOpen,
					IsLocked   = _isLocked,
					IsModified = db.Modified,
				});
			}
			if(list.Count == 0)
				list.Add(new DatabaseSummaryDto { Name = "(empty)", IsOpen = false });
			return list;
		}

		public int ActiveDocumentIndex { get { return Math.Max(0, _activeIndex); } }

		public PwDatabase GetActiveDatabase()
		{
			if(_activeIndex < 0 || _activeIndex >= _dbs.Count) return null;
			return _dbs[_activeIndex];
		}

		public PwDatabase GetDatabase(int index)
		{
			if(index < 0 || index >= _dbs.Count) return null;
			return _dbs[index];
		}

		public bool IsActiveDatabaseLocked { get { return _isLocked; } }

		public void SetActiveDocument(int index) { Log.Add("SetActiveDocument:" + index); }
		public void CreateNew()      { Log.Add("CreateNew"); }
		public void OpenDatabase()   { Log.Add("OpenDatabase"); }
		public void CloseDatabase()  { Log.Add("CloseDatabase"); }
		public void SaveDatabase()   { Log.Add("SaveDatabase"); }
		public void LockWorkspace()  { Log.Add("LockWorkspace"); }
		public void UnlockWorkspace(){ Log.Add("UnlockWorkspace"); }
	}

	// ── Fixtures ──────────────────────────────────────────────────────────────

	internal static class ViewModelFixtures
	{
		/// <summary>Open database with a two-level group hierarchy and two entries.</summary>
		public static PwDatabase DatabaseWithGroups()
		{
			var db = new PwDatabase();
			var ioc = new KeePassLib.Serialization.IOConnectionInfo();
			ioc.Path = "TestDb.kdbx";
			db.New(ioc, new KeePassLib.Keys.CompositeKey());

			var child1 = new PwGroup(true, true, "Finance", PwIcon.Folder);
			var child2 = new PwGroup(true, true, "Social",  PwIcon.Folder);
			db.RootGroup.AddGroup(child1, true);
			db.RootGroup.AddGroup(child2, true);

			var e1 = new PwEntry(true, true);
			e1.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Bank"));
			e1.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "alice"));
			e1.Strings.Set(PwDefs.UrlField,      new ProtectedString(false, "https://bank.example.com"));
			e1.AutoType.Enabled = true;
			child1.AddEntry(e1, true);

			var e2 = new PwEntry(true, true);
			e2.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Twitter"));
			e2.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "alice@tw"));
			child2.AddEntry(e2, true);

			return db;
		}

		/// <summary>Builds a ViewModel backed by <paramref name="db"/> (or empty if null).</summary>
		public static (MainWindowViewModel vm, MockSessionService svc) BuildVm(
			PwDatabase db = null, IMessenger messenger = null)
		{
			var svc = new MockSessionService();
			if(db != null) svc.SetupDatabase(db);

			var vm = new MainWindowViewModel(
				svc,
				new EntryProjectionMapper(),
				new GroupProjectionMapper(),
				messenger ?? new WeakReferenceMessenger());
			return (vm, svc);
		}
	}

	// ── Tests ─────────────────────────────────────────────────────────────────

	public class MainWindowViewModelTests
	{
		// ── Constructor null guards ───────────────────────────────────────────

		[Fact]
		public void Constructor_NullService_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(
				null, new EntryProjectionMapper(), new GroupProjectionMapper()));
		}

		[Fact]
		public void Constructor_NullEntryMapper_Throws()
		{
			var svc = new MockSessionService();
			Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(
				svc, null, new GroupProjectionMapper()));
		}

		[Fact]
		public void Constructor_NullGroupMapper_Throws()
		{
			var svc = new MockSessionService();
			Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(
				svc, new EntryProjectionMapper(), null));
		}

		// ── Initial state with no open database ───────────────────────────────

		[Fact]
		public void Constructor_NoDatabase_IsDatabaseOpenIsFalse()
		{
			var (vm, _) = ViewModelFixtures.BuildVm();
			Assert.False(vm.IsDatabaseOpen);
		}

		[Fact]
		public void Constructor_NoDatabase_GroupTreeIsEmpty()
		{
			var (vm, _) = ViewModelFixtures.BuildVm();
			Assert.Empty(vm.GroupTree);
		}

		[Fact]
		public void Constructor_NoDatabase_EntryListIsEmpty()
		{
			var (vm, _) = ViewModelFixtures.BuildVm();
			Assert.Empty(vm.EntryList);
		}

		[Fact]
		public void Constructor_NoDatabase_DatabasesContainsOnePlaceholder()
		{
			var (vm, _) = ViewModelFixtures.BuildVm();
			Assert.Single(vm.Databases);
		}

		// ── Initial state with open database ─────────────────────────────────

		[Fact]
		public void Constructor_WithDatabase_GroupTreePopulated()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);

			// Root + 2 child groups = 3
			Assert.Equal(3, vm.GroupTree.Count);
		}

		[Fact]
		public void Constructor_WithDatabase_GroupTreeFirstIsRoot()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			Assert.Equal(db.RootGroup.Uuid, vm.GroupTree[0].Uuid);
		}

		[Fact]
		public void Constructor_WithDatabase_IsDatabaseOpenIsTrue()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			Assert.True(vm.IsDatabaseOpen);
		}

		[Fact]
		public void Constructor_WithDatabase_EntryListShowsRootEntries()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			// Root group has no direct entries (entries are in child groups)
			Assert.Empty(vm.EntryList);
		}

		// ── Group selection filters EntryList ─────────────────────────────────

		[Fact]
		public void SelectedGroup_SetToFinanceGroup_EntryListContainsBankEntry()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);

			// Finance is at index 1 in GroupTree (root is 0, Finance is 1)
			GroupProjection financeGroup = vm.GroupTree[1];
			vm.SelectedGroup = financeGroup;

			Assert.Single(vm.EntryList);
			Assert.Equal("Bank", vm.EntryList[0].Title.ReadString());
		}

		[Fact]
		public void SelectedGroup_SetToSocialGroup_EntryListContainsTwitterEntry()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);

			GroupProjection socialGroup = vm.GroupTree[2];
			vm.SelectedGroup = socialGroup;

			Assert.Single(vm.EntryList);
			Assert.Equal("Twitter", vm.EntryList[0].Title.ReadString());
		}

		[Fact]
		public void SelectedGroup_SetToNull_EntryListShowsRootEntries()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);

			vm.SelectedGroup = vm.GroupTree[1]; // Finance
			vm.SelectedGroup = null;            // Back to root

			Assert.Empty(vm.EntryList); // Root has no direct entries
		}

		// ── Property-change notifications ─────────────────────────────────────

		[Fact]
		public void SelectedGroup_Change_RaisesPropertyChanged()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);

			var raised = new List<string>();
			((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

			bool entryListChanged = false;
			((INotifyCollectionChanged)vm.EntryList).CollectionChanged += (s, e) =>
				entryListChanged = true;

			vm.SelectedGroup = vm.GroupTree[1];

			Assert.Contains("SelectedGroup", raised);
			// EntryList ObservableCollection fires CollectionChanged, not PropertyChanged
			Assert.True(entryListChanged, "EntryList should have been modified when group changes.");
		}

		[Fact]
		public void SelectedEntries_Change_RaisesCanCopyPropertyChanged()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			vm.SelectedGroup = vm.GroupTree[1];

			var raised = new List<string>();
			((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

			vm.SelectedEntries = new System.Collections.ObjectModel.ObservableCollection<EntryProjection>
			{
				vm.EntryList[0]
			};

			Assert.Contains("CanCopyPassword", raised);
			Assert.Contains("CanCopyUserName", raised);
		}

		// ── Derived properties ────────────────────────────────────────────────

		[Fact]
		public void CanCopyPassword_WithSingleSelection_IsTrue()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			vm.SelectedGroup = vm.GroupTree[1];
			vm.SelectedEntries = new System.Collections.ObjectModel.ObservableCollection<EntryProjection>
			{
				vm.EntryList[0]
			};

			Assert.True(vm.CanCopyPassword);
		}

		[Fact]
		public void CanCopyPassword_WithEmptySelection_IsFalse()
		{
			var (vm, _) = ViewModelFixtures.BuildVm();
			Assert.False(vm.CanCopyPassword);
		}

		[Fact]
		public void CanCopyUserName_WithNonEmptyUserName_IsTrue()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			vm.SelectedGroup = vm.GroupTree[1];
			vm.SelectedEntries = new System.Collections.ObjectModel.ObservableCollection<EntryProjection>
			{
				vm.EntryList[0]  // Bank entry has userName = "alice"
			};

			Assert.True(vm.CanCopyUserName);
		}

		[Fact]
		public void CanOpenUrl_WithUrl_IsTrue()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			vm.SelectedGroup = vm.GroupTree[1];
			vm.SelectedEntries = new System.Collections.ObjectModel.ObservableCollection<EntryProjection>
			{
				vm.EntryList[0]  // Bank entry has URL
			};

			Assert.True(vm.CanOpenUrl);
		}

		[Fact]
		public void CanPerformAutoType_WhenAutoTypeEnabled_IsTrue()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			vm.SelectedGroup = vm.GroupTree[1];
			vm.SelectedEntries = new System.Collections.ObjectModel.ObservableCollection<EntryProjection>
			{
				vm.EntryList[0]  // Bank entry has AutoType.Enabled = true
			};

			Assert.True(vm.CanPerformAutoType);
		}

		[Fact]
		public void EnableLockCmd_WhenDatabaseOpen_IsTrue()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db);
			Assert.True(vm.EnableLockCmd);
		}

		// ── Command delegation ────────────────────────────────────────────────

		[Fact]
		public void NewDatabaseCommand_Execute_CallsServiceCreateNew()
		{
			var (vm, svc) = ViewModelFixtures.BuildVm();
			vm.NewDatabaseCommand.Execute(null);
			Assert.Contains("CreateNew", svc.Log);
		}

		[Fact]
		public void OpenDatabaseCommand_Execute_CallsServiceOpenDatabase()
		{
			var (vm, svc) = ViewModelFixtures.BuildVm();
			vm.OpenDatabaseCommand.Execute(null);
			Assert.Contains("OpenDatabase", svc.Log);
		}

		[Fact]
		public void CloseDatabaseCommand_WhenOpen_ExecutesAndCallsClose()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, svc) = ViewModelFixtures.BuildVm(db);
			Assert.True(vm.CloseDatabaseCommand.CanExecute(null));
			vm.CloseDatabaseCommand.Execute(null);
			Assert.Contains("CloseDatabase", svc.Log);
		}

		[Fact]
		public void SaveDatabaseCommand_WhenOpen_ExecutesAndCallsSave()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, svc) = ViewModelFixtures.BuildVm(db);
			Assert.True(vm.SaveDatabaseCommand.CanExecute(null));
			vm.SaveDatabaseCommand.Execute(null);
			Assert.Contains("SaveDatabase", svc.Log);
		}

		[Fact]
		public void LockWorkspaceCommand_WhenOpen_ExecutesAndCallsLock()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, svc) = ViewModelFixtures.BuildVm(db);
			Assert.True(vm.LockWorkspaceCommand.CanExecute(null));
			vm.LockWorkspaceCommand.Execute(null);
			Assert.Contains("LockWorkspace", svc.Log);
		}

		[Fact]
		public void UnlockWorkspaceCommand_WhenLocked_ExecutesAndCallsUnlock()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, svc) = ViewModelFixtures.BuildVm(db);
			svc.SetLocked(true);
			svc.SimulateSessionChanged();

			Assert.True(vm.IsLocked);
			vm.UnlockWorkspaceCommand.Execute(null);
			Assert.Contains("UnlockWorkspace", svc.Log);
		}

		// ── SessionChanged refresh ────────────────────────────────────────────

		[Fact]
		public void SessionChanged_FiresAndRefreshesGroupTree()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, svc) = ViewModelFixtures.BuildVm(db);

			int initialCount = vm.GroupTree.Count;
			db.RootGroup.AddGroup(new PwGroup(true, true, "New", PwIcon.Folder), true);
			svc.SimulateSessionChanged();

			Assert.Equal(initialCount + 1, vm.GroupTree.Count);
		}

		[Fact]
		public void SessionChanged_IsLocked_PropertyUpdated()
		{
			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, svc) = ViewModelFixtures.BuildVm(db);
			Assert.False(vm.IsLocked);

			svc.SetLocked(true);
			svc.SimulateSessionChanged();

			Assert.True(vm.IsLocked);
		}

		// ── WeakReferenceMessenger messages ───────────────────────────────────

		[Fact]
		public void DatabaseChangedMessage_SentOnConstructionWithOpenDb()
		{
			var messenger = new WeakReferenceMessenger();
			DatabaseChangedMessage received = null;
			messenger.Register<DatabaseChangedMessage>(this, (r, m) => received = m);

			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			ViewModelFixtures.BuildVm(db, messenger);

			Assert.NotNull(received);
			Assert.Equal(db, received.ActiveDatabase);
		}

		[Fact]
		public void GroupSelectedMessage_SentWhenSelectedGroupChanges()
		{
			var messenger = new WeakReferenceMessenger();
			GroupSelectedMessage received = null;
			messenger.Register<GroupSelectedMessage>(this, (r, m) => received = m);

			PwDatabase db = ViewModelFixtures.DatabaseWithGroups();
			var (vm, _) = ViewModelFixtures.BuildVm(db, messenger);

			vm.SelectedGroup = vm.GroupTree[1];

			Assert.NotNull(received);
			Assert.Equal(vm.GroupTree[1].Uuid, received.Group.Uuid);
		}

		// ── No WinForms dependency ────────────────────────────────────────────

		[Fact]
		public void MainWindowViewModel_Assembly_HasNoSystemWindowsFormsReference()
		{
			System.Reflection.Assembly coreAssembly = typeof(MainWindowViewModel).Assembly;
			foreach(System.Reflection.AssemblyName refName in coreAssembly.GetReferencedAssemblies())
			{
				Assert.False(
					refName.Name.Equals("System.Windows.Forms",
						System.StringComparison.OrdinalIgnoreCase),
					$"KeePass.Core should not reference '{refName.Name}'.");
			}
		}
	}
}
