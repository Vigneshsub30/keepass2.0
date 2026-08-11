using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using KeePass.Core.Projections;
using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Unit tests for <see cref="SearchViewModel"/>.
	/// All searches run on an in-memory <see cref="PwGroup"/> tree — no
	/// external dependencies, no WinForms references.
	/// </summary>
	public sealed class SearchViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Fixtures                                                              //
		// ------------------------------------------------------------------ //

		private static EntryProjectionMapper Mapper() => new EntryProjectionMapper();

		/// <summary>
		/// Builds a small in-memory database with known entries:
		///   "GitHub login"   — user "alice", url "https://github.com", tag "dev"
		///   "Gmail account"  — user "alice@example.com", tag "email"
		///   "Secret server"  — user "admin", tag "server"
		/// </summary>
		private static PwGroup BuildRootGroup()
		{
			var root = new PwGroup(true, true, "Root", PwIcon.Folder);

			root.AddEntry(MakeEntry("GitHub login", "alice", "pass1",
				"https://github.com", "GitHub note", new[] { "dev" }), true);
			root.AddEntry(MakeEntry("Gmail account", "alice@example.com", "pass2",
				"https://gmail.com", "Gmail note", new[] { "email" }), true);
			root.AddEntry(MakeEntry("Secret server", "admin", "pass3",
				"https://internal/", "Server note", new[] { "server" }), true);

			return root;
		}

		private static PwEntry MakeEntry(
			string title, string user, string password, string url, string notes,
			string[] tags)
		{
			var e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));
			e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, user));
			e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, password));
			e.Strings.Set(PwDefs.UrlField, new ProtectedString(false, url));
			e.Strings.Set(PwDefs.NotesField, new ProtectedString(false, notes));
			foreach (var t in tags) e.Tags.Add(t);
			return e;
		}

		private SearchViewModel MakeVm(
			PwGroup? root = null,
			ISearchProfileStore? store = null) =>
			new SearchViewModel(
				() => root ?? BuildRootGroup(),
				Mapper(),
				store ?? new InMemoryProfileStore());

		// ------------------------------------------------------------------ //
		// Stub profile store                                                    //
		// ------------------------------------------------------------------ //

		private sealed class InMemoryProfileStore : ISearchProfileStore
		{
			private readonly Dictionary<string, SearchParameters> _profiles =
				new Dictionary<string, SearchParameters>(StringComparer.OrdinalIgnoreCase);

			public IReadOnlyList<SearchParameters> GetProfiles() =>
				_profiles.Values.ToList();

			public void SaveProfile(SearchParameters profile) =>
				_profiles[profile.Name] = profile;

			public void DeleteProfile(string name) => _profiles.Remove(name);
		}

		// ------------------------------------------------------------------ //
		// Constructor tests                                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Constructor_NullRootProvider_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new SearchViewModel(null!, Mapper(), new InMemoryProfileStore()));
		}

		[Fact]
		public void Constructor_NullMapper_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new SearchViewModel(() => BuildRootGroup(), null!, new InMemoryProfileStore()));
		}

		[Fact]
		public void Constructor_NullProfileStore_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new SearchViewModel(() => BuildRootGroup(), Mapper(), null!));
		}

		// ------------------------------------------------------------------ //
		// Default state                                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void InitialState_DefaultValuesMatchSearchParameters()
		{
			var vm = MakeVm();
			var sp = new SearchParameters(); // defaults

			Assert.Equal(sp.SearchString, vm.SearchString);
			Assert.Equal(sp.SearchMode, vm.SearchMode);
			Assert.Equal(sp.SearchInTitles, vm.SearchInTitles);
			Assert.Equal(sp.SearchInUserNames, vm.SearchInUserNames);
			Assert.Equal(sp.SearchInPasswords, vm.SearchInPasswords);
			Assert.Equal(sp.SearchInUrls, vm.SearchInUrls);
			Assert.Equal(sp.SearchInNotes, vm.SearchInNotes);
			Assert.Equal(sp.SearchInOther, vm.SearchInOther);
			Assert.Equal(sp.SearchInStringNames, vm.SearchInStringNames);
			Assert.Equal(sp.SearchInTags, vm.SearchInTags);
			Assert.Equal(sp.SearchInUuids, vm.SearchInUuids);
			Assert.Equal(sp.SearchInGroupPaths, vm.SearchInGroupPaths);
			Assert.Equal(sp.SearchInGroupNames, vm.SearchInGroupNames);
			Assert.Equal(sp.SearchInHistory, vm.SearchInHistory);
			Assert.Equal(sp.ComparisonMode, vm.ComparisonMode);
			Assert.Equal(sp.MatchDiacritics, vm.MatchDiacritics);
			Assert.Equal(sp.ExcludeExpired, vm.ExcludeExpired);
			Assert.Equal(sp.RespectEntrySearchingDisabled, vm.RespectEntrySearchingDisabled);
		}

		[Fact]
		public void InitialState_ResultsEmpty_CountZero()
		{
			var vm = MakeVm();
			Assert.Empty(vm.SearchResults);
			Assert.Equal(0, vm.ResultCount);
		}

		// ------------------------------------------------------------------ //
		// SearchCommand.CanExecute                                             //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SearchCommand_EmptySearchString_CannotExecute()
		{
			var vm = MakeVm();
			Assert.False(vm.SearchCommand.CanExecute(null));
		}

		[Fact]
		public void SearchCommand_WhitespaceSearchString_CannotExecute()
		{
			var vm = MakeVm();
			vm.SearchString = "   ";
			Assert.False(vm.SearchCommand.CanExecute(null));
		}

		[Fact]
		public void SearchCommand_NonEmptySearchString_CanExecute()
		{
			var vm = MakeVm();
			vm.SearchString = "GitHub";
			Assert.True(vm.SearchCommand.CanExecute(null));
		}

		[Fact]
		public void SearchString_Change_RaisesCanExecuteChanged()
		{
			var vm = MakeVm();
			bool fired = false;
			vm.SearchCommand.CanExecuteChanged += (_, _) => fired = true;

			vm.SearchString = "test";

			Assert.True(fired);
		}

		// ------------------------------------------------------------------ //
		// Search execution — title search                                      //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task SearchCommand_TitleSearch_ReturnsMatchingEntries()
		{
			var vm = MakeVm();
			vm.SearchString = "GitHub";
			vm.SearchInTitles = true;
			vm.SearchInUserNames = false;
			vm.SearchInUrls = false;
			vm.SearchInNotes = false;
			vm.SearchInOther = false;
			vm.SearchInTags = false;

			await vm.SearchCommand.ExecuteAsync(null);

			Assert.Single(vm.SearchResults);
			Assert.Equal("GitHub login", vm.SearchResults[0].Title.ReadString());
			Assert.Equal(1, vm.ResultCount);
		}

		[Fact]
		public async Task SearchCommand_TitleSearch_ReturnsAllMatches()
		{
			var vm = MakeVm();
			// "G" (case-insensitive) matches "GitHub login" and "Gmail account" but not "Secret server".
			vm.SearchString = "G";
			vm.SearchInTitles = true;
			vm.SearchInUserNames = false;
			vm.SearchInUrls = false;
			vm.SearchInNotes = false;
			vm.SearchInOther = false;
			vm.SearchInTags = false;

			await vm.SearchCommand.ExecuteAsync(null);

			Assert.Equal(2, vm.ResultCount);
		}

		// ------------------------------------------------------------------ //
		// Search execution — user name search                                  //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task SearchCommand_UserNameSearch_FindsUserByExactSubstring()
		{
			var vm = MakeVm();
			vm.SearchString = "alice@example";
			vm.SearchInTitles = false;
			vm.SearchInUserNames = true;
			vm.SearchInUrls = false;
			vm.SearchInNotes = false;
			vm.SearchInOther = false;
			vm.SearchInTags = false;

			await vm.SearchCommand.ExecuteAsync(null);

			Assert.Single(vm.SearchResults);
			Assert.Equal("Gmail account", vm.SearchResults[0].Title.ReadString());
		}

		// ------------------------------------------------------------------ //
		// Search execution — regex search                                      //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task SearchCommand_RegexSearch_MatchesPattern()
		{
			var vm = MakeVm();
			vm.SearchString = "^G";         // starts with G
			vm.SearchMode = PwSearchMode.Regular;
			vm.SearchInTitles = true;
			vm.SearchInUserNames = false;
			vm.SearchInUrls = false;
			vm.SearchInNotes = false;
			vm.SearchInOther = false;
			vm.SearchInTags = false;

			await vm.SearchCommand.ExecuteAsync(null);

			// "GitHub login" and "Gmail account" both start with G
			Assert.Equal(2, vm.ResultCount);
		}

		// ------------------------------------------------------------------ //
		// Search execution — result population                                 //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task SearchCommand_MultipleResults_SearchResultsCollectionPopulated()
		{
			var vm = MakeVm();
			vm.SearchString = "note"; // all entries have "note" in their notes field
			vm.SearchInTitles = false;
			vm.SearchInUserNames = false;
			vm.SearchInUrls = false;
			vm.SearchInNotes = true;
			vm.SearchInOther = false;
			vm.SearchInTags = false;

			await vm.SearchCommand.ExecuteAsync(null);

			Assert.Equal(3, vm.SearchResults.Count);
			Assert.Equal(3, vm.ResultCount);
		}

		[Fact]
		public async Task SearchCommand_SecondSearch_ClearsPreviousResults()
		{
			var vm = MakeVm();
			vm.SearchString = "GitHub";
			await vm.SearchCommand.ExecuteAsync(null);
			Assert.Equal(1, vm.ResultCount);

			vm.SearchString = "NORESULT_XYZZY";
			await vm.SearchCommand.ExecuteAsync(null);

			Assert.Equal(0, vm.ResultCount);
			Assert.Empty(vm.SearchResults);
		}

		// ------------------------------------------------------------------ //
		// BuildSearchParameters / ApplySearchParameters round-trip             //
		// ------------------------------------------------------------------ //

		[Fact]
		public void BuildSearchParameters_ReflectsAllCurrentProperties()
		{
			var vm = MakeVm();
			vm.SearchString = "test";
			vm.SearchMode = PwSearchMode.Regular;
			vm.SearchInTitles = false;
			vm.SearchInUserNames = false;
			vm.SearchInPasswords = true;
			vm.SearchInUrls = false;
			vm.SearchInNotes = false;
			vm.SearchInOther = false;
			vm.SearchInStringNames = true;
			vm.SearchInTags = false;
			vm.SearchInUuids = true;
			vm.SearchInGroupPaths = true;
			vm.SearchInGroupNames = true;
			vm.SearchInHistory = true;
			vm.ComparisonMode = StringComparison.Ordinal;
			vm.MatchDiacritics = true;
			vm.ExcludeExpired = true;
			vm.RespectEntrySearchingDisabled = false;

			SearchParameters sp = vm.BuildSearchParameters();

			Assert.Equal("test", sp.SearchString);
			Assert.Equal(PwSearchMode.Regular, sp.SearchMode);
			Assert.False(sp.SearchInTitles);
			Assert.False(sp.SearchInUserNames);
			Assert.True(sp.SearchInPasswords);
			Assert.False(sp.SearchInUrls);
			Assert.False(sp.SearchInNotes);
			Assert.False(sp.SearchInOther);
			Assert.True(sp.SearchInStringNames);
			Assert.False(sp.SearchInTags);
			Assert.True(sp.SearchInUuids);
			Assert.True(sp.SearchInGroupPaths);
			Assert.True(sp.SearchInGroupNames);
			Assert.True(sp.SearchInHistory);
			Assert.Equal(StringComparison.Ordinal, sp.ComparisonMode);
			Assert.True(sp.MatchDiacritics);
			Assert.True(sp.ExcludeExpired);
			Assert.False(sp.RespectEntrySearchingDisabled);
		}

		[Fact]
		public void ApplySearchParameters_PopulatesAllProperties()
		{
			var vm = MakeVm();
			var sp = new SearchParameters
			{
				SearchString = "apply_test",
				SearchMode = PwSearchMode.XPath,
				SearchInTitles = false,
				SearchInUserNames = false,
				SearchInPasswords = true,
				SearchInUrls = false,
				SearchInNotes = false,
				SearchInOther = false,
				SearchInStringNames = true,
				SearchInTags = false,
				SearchInUuids = true,
				SearchInGroupPaths = true,
				SearchInGroupNames = true,
				SearchInHistory = true,
				ComparisonMode = StringComparison.InvariantCulture,
				MatchDiacritics = true,
				ExcludeExpired = true,
				RespectEntrySearchingDisabled = false
			};

			vm.ApplySearchParameters(sp);

			Assert.Equal("apply_test", vm.SearchString);
			Assert.Equal(PwSearchMode.XPath, vm.SearchMode);
			Assert.False(vm.SearchInTitles);
			Assert.False(vm.SearchInUserNames);
			Assert.True(vm.SearchInPasswords);
			Assert.False(vm.SearchInUrls);
			Assert.False(vm.SearchInNotes);
			Assert.False(vm.SearchInOther);
			Assert.True(vm.SearchInStringNames);
			Assert.False(vm.SearchInTags);
			Assert.True(vm.SearchInUuids);
			Assert.True(vm.SearchInGroupPaths);
			Assert.True(vm.SearchInGroupNames);
			Assert.True(vm.SearchInHistory);
			Assert.Equal(StringComparison.InvariantCulture, vm.ComparisonMode);
			Assert.True(vm.MatchDiacritics);
			Assert.True(vm.ExcludeExpired);
			Assert.False(vm.RespectEntrySearchingDisabled);
		}

		[Fact]
		public void ApplySearchParameters_Null_ThrowsArgumentNullException()
		{
			var vm = MakeVm();
			Assert.Throws<ArgumentNullException>(() => vm.ApplySearchParameters(null!));
		}

		// ------------------------------------------------------------------ //
		// Profile CRUD                                                         //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SaveProfileCommand_NewProfile_AppearsInSearchProfiles()
		{
			var vm = MakeVm();
			vm.SearchString = "github";
			vm.SearchInNotes = false;

			vm.SaveProfileCommand.Execute("DevSearch");

			Assert.Single(vm.SearchProfiles);
			Assert.Equal("DevSearch", vm.SearchProfiles[0].Name);
		}

		[Fact]
		public void SaveProfileCommand_SameName_ReplacesExistingProfile()
		{
			var vm = MakeVm();
			vm.SearchString = "v1";
			vm.SaveProfileCommand.Execute("MyProfile");

			vm.SearchString = "v2";
			vm.SaveProfileCommand.Execute("MyProfile");

			Assert.Single(vm.SearchProfiles);
			Assert.Equal("v2", vm.SearchProfiles[0].Parameters.SearchString);
		}

		[Fact]
		public void LoadProfileCommand_RestoresParameters()
		{
			var vm = MakeVm();
			vm.SearchString = "load_test";
			vm.SearchInNotes = false;
			vm.SaveProfileCommand.Execute("Loaded");

			vm.SearchString = string.Empty;
			vm.SearchInNotes = true;

			vm.LoadProfileCommand.Execute(vm.SearchProfiles[0]);

			Assert.Equal("load_test", vm.SearchString);
			Assert.False(vm.SearchInNotes);
			Assert.Equal(vm.SearchProfiles[0], vm.SelectedProfile);
		}

		[Fact]
		public void DeleteProfileCommand_RemovesProfileFromCollection()
		{
			var vm = MakeVm();
			vm.SearchString = "x";
			vm.SaveProfileCommand.Execute("ToDelete");

			var dto = vm.SearchProfiles[0];
			vm.DeleteProfileCommand.Execute(dto);

			Assert.Empty(vm.SearchProfiles);
		}

		[Fact]
		public void DeleteProfileCommand_SelectedProfile_ClearsSelection()
		{
			var vm = MakeVm();
			vm.SearchString = "x";
			vm.SaveProfileCommand.Execute("P");
			vm.LoadProfileCommand.Execute(vm.SearchProfiles[0]);

			var dto = vm.SearchProfiles[0];
			vm.DeleteProfileCommand.Execute(dto);

			Assert.Null(vm.SelectedProfile);
		}

		[Fact]
		public void SaveProfileCommand_NullOrWhitespaceName_DoesNothing()
		{
			var vm = MakeVm();
			vm.SearchString = "x";

			vm.SaveProfileCommand.Execute(null);
			vm.SaveProfileCommand.Execute("  ");

			Assert.Empty(vm.SearchProfiles);
		}

		[Fact]
		public void Constructor_LoadsExistingProfilesFromStore()
		{
			var store = new InMemoryProfileStore();
			store.SaveProfile(new SearchParameters { Name = "Pre-existing", SearchString = "pre" });

			var vm = new SearchViewModel(() => BuildRootGroup(), Mapper(), store);

			Assert.Single(vm.SearchProfiles);
			Assert.Equal("Pre-existing", vm.SearchProfiles[0].Name);
		}

		// ------------------------------------------------------------------ //
		// IsSearchInProgress transitions                                       //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task SearchCommand_DuringExecution_IsSearchInProgressIsTrue()
		{
			var vm = MakeVm();
			vm.SearchString = "a";

			bool observedInProgress = false;
			((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(SearchViewModel.IsSearchInProgress)
					&& vm.IsSearchInProgress)
					observedInProgress = true;
			};

			await vm.SearchCommand.ExecuteAsync(null);

			Assert.False(vm.IsSearchInProgress);
			Assert.True(observedInProgress);
		}

		// ------------------------------------------------------------------ //
		// No WinForms references                                               //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SearchViewModel_HasNoWinFormsReference()
		{
			var asm = typeof(SearchViewModel).Assembly;
			foreach (var refName in asm.GetReferencedAssemblies())
			{
				Assert.DoesNotContain("System.Windows.Forms", refName.FullName,
					StringComparison.OrdinalIgnoreCase);
			}
		}
	}
}
