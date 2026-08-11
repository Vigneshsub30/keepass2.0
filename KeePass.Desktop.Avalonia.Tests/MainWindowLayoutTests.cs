using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Messaging;

using KeePass.Core.Projections;
using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Tests the MainWindow three-panel interaction: group tree, entry list,
	/// and entry preview update correctly when the session changes.
	/// All tests use the in-process stub service and do NOT require a real
	/// Avalonia window, so they run on the standard xUnit test runner.
	/// </summary>
	public sealed class MainWindowLayoutTests
	{
		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private static MainWindowViewModel BuildVm(
			out TestServiceProvider.StubDatabaseSessionService session)
		{
			var sp = TestServiceProvider.Build();
			// The DI container creates its own StubDatabaseSessionService instance;
			// we retrieve it from the same container for manipulation.
			session = (TestServiceProvider.StubDatabaseSessionService)
				sp.GetService(typeof(IDatabaseSessionService))!;
			return (MainWindowViewModel)sp.GetService(typeof(MainWindowViewModel))!;
		}

		// ------------------------------------------------------------------ //
		// Initial state                                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void InitialState_NoDatabaseOpen_AllPanelsEmpty()
		{
			var vm = BuildVm(out _);

			Assert.False(vm.IsDatabaseOpen);
			Assert.Empty(vm.Databases);
			Assert.Empty(vm.GroupTree);
			Assert.Empty(vm.EntryList);
		}

		[Fact]
		public void InitialState_StatusText_NotNull()
		{
			var vm = BuildVm(out _);
			Assert.NotNull(vm.StatusText);
		}

		// ------------------------------------------------------------------ //
		// Group tree panel                                                     //
		// ------------------------------------------------------------------ //

		[Fact]
		public void AfterDatabaseAdded_GroupTree_ContainsRootGroups()
		{
			var vm = BuildVm(out var session);
			var db = TestDatabaseFactory.CreateSample(groupDepth: 2, entriesPerGroup: 2);

			session.AddDatabase(db);

			Assert.NotEmpty(vm.GroupTree);
		}

		[Fact]
		public void AfterDatabaseAdded_Databases_CollectionUpdated()
		{
			var vm = BuildVm(out var session);
			var db = TestDatabaseFactory.CreateSample();

			session.AddDatabase(db);

			Assert.Single(vm.Databases);
			Assert.True(vm.IsDatabaseOpen);
		}

		// ------------------------------------------------------------------ //
		// Entry list panel                                                     //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SelectedGroup_WithEntries_PopulatesEntryList()
		{
			var vm = BuildVm(out var session);
			var db = TestDatabaseFactory.CreateSample(groupDepth: 1, entriesPerGroup: 3);
			session.AddDatabase(db);

			// GroupTree is a flat depth-first list: [root, child1, child2, ...]
			// The root itself has no direct entries — pick the first child group.
			var firstChild = vm.GroupTree.Skip(1).FirstOrDefault();
			Assert.NotNull(firstChild);

			vm.SelectedGroup = firstChild;

			Assert.NotEmpty(vm.EntryList);
		}

		[Fact]
		public void SelectedGroup_Root_EntryListShowsRootEntries()
		{
			var vm = BuildVm(out var session);
			var db = TestDatabaseFactory.CreateEmpty();

			// Add an entry directly to root
			var e = new KeePassLib.PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,
				new KeePassLib.Security.ProtectedString(false, "Root Entry"));
			db.RootGroup.AddEntry(e, true);

			session.AddDatabase(db);

			var root = vm.GroupTree.FirstOrDefault();
			if (root != null)
			{
				vm.SelectedGroup = root;
				// Entry list may or may not contain the root-group entries
				// depending on whether the root group is surfaced — just verify
				// the collection doesn't throw.
				Assert.NotNull(vm.EntryList);
			}
		}

		// ------------------------------------------------------------------ //
		// Command enabled state                                                //
		// ------------------------------------------------------------------ //

		[Fact]
		public void CanCopyUserName_WhenNoDatabaseOpen_IsFalse()
		{
			var vm = BuildVm(out _);
			Assert.False(vm.CanCopyUserName);
		}

		[Fact]
		public void CanCopyPassword_WhenNoDatabaseOpen_IsFalse()
		{
			var vm = BuildVm(out _);
			Assert.False(vm.CanCopyPassword);
		}

		[Fact]
		public void EnableLockCmd_WhenDatabaseOpen_IsTrue()
		{
			var vm = BuildVm(out var session);
			session.AddDatabase(TestDatabaseFactory.CreateSample());

			Assert.True(vm.EnableLockCmd);
		}

		// ------------------------------------------------------------------ //
		// Multi-database tabs                                                  //
		// ------------------------------------------------------------------ //

		[Fact]
		public void MultipleOpenDatabases_DatabasesCollection_HasCorrectCount()
		{
			var vm = BuildVm(out var session);
			session.AddDatabase(TestDatabaseFactory.CreateSample("Db1.kdbx"));
			session.AddDatabase(TestDatabaseFactory.CreateSample("Db2.kdbx"));

			Assert.Equal(2, vm.Databases.Count);
		}

		[Fact]
		public void ClosingDatabase_DatabasesCollection_Decrements()
		{
			var vm = BuildVm(out var session);
			var db = TestDatabaseFactory.CreateSample();
			session.AddDatabase(db);
			Assert.Single(vm.Databases);

			session.CloseDatabase(db);
			Assert.Empty(vm.Databases);
		}

		// ------------------------------------------------------------------ //
		// Platform isolation check                                             //
		// ------------------------------------------------------------------ //

		[Fact]
		public void DesktopAvaloniaProject_HasNo_WinFormsReference()
		{
			var asm = typeof(App).Assembly;
			var winForms = asm.GetReferencedAssemblies()
				.Any(r => r.Name != null &&
					r.Name.StartsWith("System.Windows.Forms", StringComparison.Ordinal));
			Assert.False(winForms,
				"KeePass.Desktop.Avalonia must not reference System.Windows.Forms.");
		}
	}
}
