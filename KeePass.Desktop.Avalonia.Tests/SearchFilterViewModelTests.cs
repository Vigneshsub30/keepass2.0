using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KeePass.Core.Projections;
using KeePass.Core.ViewModels;

using KeePassLib;
using KeePassLib.Security;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Unit tests for <see cref="TagFilterViewModel"/> and
	/// <see cref="ExpiryFilterViewModel"/>.
	/// </summary>
	public sealed class SearchFilterViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private static EntryProjectionMapper Mapper() => new EntryProjectionMapper();

		private static PwGroup BuildTaggedDatabase()
		{
			// Root: Finance (tag: finance)
			//   ├── Gmail    (tag: email, finance)
			//   ├── Bank     (tag: finance)
			//   └── Personal (tag: personal)
			var root = new PwGroup(true, true, "Root", PwIcon.Folder);

			var gmail = MakeEntry("Gmail", "user@gmail.com", tags: new[] { "email", "finance" });
			var bank  = MakeEntry("Bank Login", "bankuser",   tags: new[] { "finance" });
			var pers  = MakeEntry("Personal",  "me",          tags: new[] { "personal" });

			root.AddEntry(gmail, true);
			root.AddEntry(bank,  true);
			root.AddEntry(pers,  true);
			return root;
		}

		private static PwGroup BuildExpiryDatabase()
		{
			var root = new PwGroup(true, true, "Root", PwIcon.Folder);

			var expired = MakeEntry("Expired", "user");
			expired.Expires    = true;
			expired.ExpiryTime = DateTime.UtcNow.AddDays(-5);

			var soonExpiring = MakeEntry("Expiring Soon", "user");
			soonExpiring.Expires    = true;
			soonExpiring.ExpiryTime = DateTime.UtcNow.AddDays(3);

			var farExpiring = MakeEntry("Far Future", "user");
			farExpiring.Expires    = true;
			farExpiring.ExpiryTime = DateTime.UtcNow.AddDays(60);

			var noExpiry = MakeEntry("No Expiry", "user");

			root.AddEntry(expired,      true);
			root.AddEntry(soonExpiring, true);
			root.AddEntry(farExpiring,  true);
			root.AddEntry(noExpiry,     true);

			return root;
		}

		private static PwEntry MakeEntry(string title, string user, string[]? tags = null)
		{
			var e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, title));
			e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, user));
			if (tags != null)
				e.Tags.AddRange(tags);
			return e;
		}

		// ================================================================== //
		// TagFilterViewModel                                                   //
		// ================================================================== //

		[Fact]
		public void TagFilter_Constructor_NullMapper_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new TagFilterViewModel(null!));
		}

		[Fact]
		public void TagFilter_LoadTags_PopulatesAvailableTags()
		{
			var vm   = new TagFilterViewModel(Mapper());
			var root = BuildTaggedDatabase();

			vm.LoadTags(root);

			Assert.Contains("email",    vm.AvailableTags);
			Assert.Contains("finance",  vm.AvailableTags);
			Assert.Contains("personal", vm.AvailableTags);
		}

		[Fact]
		public void TagFilter_LoadTags_Null_ClearsAvailableTags()
		{
			var vm   = new TagFilterViewModel(Mapper());
			vm.LoadTags(BuildTaggedDatabase());
			Assert.NotEmpty(vm.AvailableTags);

			vm.LoadTags(null);
			Assert.Empty(vm.AvailableTags);
		}

		[Fact]
		public void TagFilter_FilterCommand_InitiallyCannotExecute()
		{
			var vm = new TagFilterViewModel(Mapper());
			Assert.False(vm.FilterCommand.CanExecute(null));
		}

		[Fact]
		public void TagFilter_SelectedTag_Set_FilterCommandCanExecute()
		{
			var vm   = new TagFilterViewModel(Mapper());
			vm.LoadTags(BuildTaggedDatabase());
			vm.SelectedTag = "finance";

			Assert.True(vm.FilterCommand.CanExecute(null));
		}

		[Fact]
		public async Task TagFilter_Filter_FinanceTag_ReturnsTwoEntries()
		{
			var vm   = new TagFilterViewModel(Mapper());
			var root = BuildTaggedDatabase();
			vm.LoadTags(root);
			vm.SelectedTag = "finance";

			IReadOnlyList<EntryProjection>? results = null;
			vm.ResultsReady += (_, r) => results = r;

			await vm.FilterCommand.ExecuteAsync(null);

			Assert.NotNull(results);
			Assert.Equal(2, results!.Count);
		}

		[Fact]
		public async Task TagFilter_Filter_PersonalTag_ReturnsOneEntry()
		{
			var vm   = new TagFilterViewModel(Mapper());
			var root = BuildTaggedDatabase();
			vm.LoadTags(root);
			vm.SelectedTag = "personal";

			IReadOnlyList<EntryProjection>? results = null;
			vm.ResultsReady += (_, r) => results = r;

			await vm.FilterCommand.ExecuteAsync(null);

			Assert.NotNull(results);
			Assert.Single(results!);
		}

		[Fact]
		public void TagFilter_Clear_ResetsSelectedTag()
		{
			var vm = new TagFilterViewModel(Mapper());
			vm.LoadTags(BuildTaggedDatabase());
			vm.SelectedTag = "finance";

			vm.ClearCommand.Execute(null);

			Assert.Null(vm.SelectedTag);
		}

		// ================================================================== //
		// ExpiryFilterViewModel                                                //
		// ================================================================== //

		[Fact]
		public void ExpiryFilter_Constructor_NullMapper_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new ExpiryFilterViewModel(null!));
		}

		[Fact]
		public void ExpiryFilter_Periods_ContainsPredefinedOptions()
		{
			var vm = new ExpiryFilterViewModel(Mapper());
			Assert.True(vm.Periods.Count >= 4);
		}

		[Fact]
		public void ExpiryFilter_FilterCommand_InitiallyCannotExecute()
		{
			var vm = new ExpiryFilterViewModel(Mapper());
			Assert.False(vm.FilterCommand.CanExecute(null));
		}

		[Fact]
		public void ExpiryFilter_SelectedPeriod_Set_FilterCommandCanExecute()
		{
			var vm = new ExpiryFilterViewModel(Mapper());
			vm.SelectedPeriod = vm.Periods[0];
			Assert.True(vm.FilterCommand.CanExecute(null));
		}

		[Fact]
		public async Task ExpiryFilter_AlreadyExpired_FindsExpiredEntry()
		{
			var vm   = new ExpiryFilterViewModel(Mapper());
			var root = BuildExpiryDatabase();
			vm.LoadGroup(root);

			// "Already expired" is the first period (DaysAhead = null).
			vm.SelectedPeriod = vm.Periods[0];

			IReadOnlyList<EntryProjection>? results = null;
			vm.ResultsReady += (_, r) => results = r;

			await vm.FilterCommand.ExecuteAsync(null);

			Assert.NotNull(results);
			Assert.Single(results!);
		}

		[Fact]
		public async Task ExpiryFilter_Next7Days_FindsExpiredAndSoon()
		{
			var vm   = new ExpiryFilterViewModel(Mapper());
			var root = BuildExpiryDatabase();
			vm.LoadGroup(root);

			// "Expires in 7 days" is the second period (DaysAhead = 7).
			vm.SelectedPeriod = vm.Periods[1];

			IReadOnlyList<EntryProjection>? results = null;
			vm.ResultsReady += (_, r) => results = r;

			await vm.FilterCommand.ExecuteAsync(null);

			// Expired (-5 days) and soon expiring (+3 days) should both be included.
			Assert.NotNull(results);
			Assert.Equal(2, results!.Count);
		}

		[Fact]
		public void ExpiryFilter_Clear_ResetsSelectedPeriod()
		{
			var vm = new ExpiryFilterViewModel(Mapper());
			vm.SelectedPeriod = vm.Periods[0];

			vm.ClearCommand.Execute(null);

			Assert.Null(vm.SelectedPeriod);
		}

		// ================================================================== //
		// MainWindowViewModel QuickSearch                                      //
		// ================================================================== //

		[Fact]
		public void MainWindowViewModel_QuickSearchText_NotifiesPropertyChanged()
		{
			var vm = TestServiceProvider.Build()
				.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
			Assert.NotNull(vm);

			var changed = new System.Collections.Generic.List<string?>();
			vm!.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

			vm.QuickSearchText = "test";

			Assert.Contains(nameof(MainWindowViewModel.QuickSearchText), changed);
		}
	}
}
