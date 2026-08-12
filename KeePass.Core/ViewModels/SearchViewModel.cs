using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePass.Core.Projections;
using KeePass.Core.Services;

using KeePassLib;
using KeePassLib.Interfaces;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for the search dialog.  Wraps all <see cref="SearchParameters"/>
	/// fields as observable properties, executes searches asynchronously via
	/// <see cref="PwGroup.SearchEntries"/>, and manages named search profiles.
	/// No WinForms references.
	/// </summary>
	public sealed class SearchViewModel : ObservableObject
	{
		private readonly Func<PwGroup> _rootGroupProvider;
		private readonly EntryProjectionMapper _mapper;
		private readonly ISearchProfileStore _profileStore;

		// ------------------------------------------------------------------ //
		// Search fields — mirror all SearchParameters properties              //
		// ------------------------------------------------------------------ //

		private string _searchString = string.Empty;
		public string SearchString
		{
			get => _searchString;
			set
			{
				if (SetProperty(ref _searchString, value ?? string.Empty))
					SearchCommand.NotifyCanExecuteChanged();
			}
		}

		private PwSearchMode _searchMode = PwSearchMode.Simple;
		public PwSearchMode SearchMode
		{
			get => _searchMode;
			set => SetProperty(ref _searchMode, value);
		}

		private bool _searchInTitles = true;
		public bool SearchInTitles
		{
			get => _searchInTitles;
			set => SetProperty(ref _searchInTitles, value);
		}

		private bool _searchInUserNames = true;
		public bool SearchInUserNames
		{
			get => _searchInUserNames;
			set => SetProperty(ref _searchInUserNames, value);
		}

		private bool _searchInPasswords;
		public bool SearchInPasswords
		{
			get => _searchInPasswords;
			set => SetProperty(ref _searchInPasswords, value);
		}

		private bool _searchInUrls = true;
		public bool SearchInUrls
		{
			get => _searchInUrls;
			set => SetProperty(ref _searchInUrls, value);
		}

		private bool _searchInNotes = true;
		public bool SearchInNotes
		{
			get => _searchInNotes;
			set => SetProperty(ref _searchInNotes, value);
		}

		private bool _searchInOther = true;
		public bool SearchInOther
		{
			get => _searchInOther;
			set => SetProperty(ref _searchInOther, value);
		}

		private bool _searchInStringNames;
		public bool SearchInStringNames
		{
			get => _searchInStringNames;
			set => SetProperty(ref _searchInStringNames, value);
		}

		private bool _searchInTags = true;
		public bool SearchInTags
		{
			get => _searchInTags;
			set => SetProperty(ref _searchInTags, value);
		}

		private bool _searchInUuids;
		public bool SearchInUuids
		{
			get => _searchInUuids;
			set => SetProperty(ref _searchInUuids, value);
		}

		private bool _searchInGroupPaths;
		public bool SearchInGroupPaths
		{
			get => _searchInGroupPaths;
			set => SetProperty(ref _searchInGroupPaths, value);
		}

		private bool _searchInGroupNames;
		public bool SearchInGroupNames
		{
			get => _searchInGroupNames;
			set => SetProperty(ref _searchInGroupNames, value);
		}

		private bool _searchInHistory;
		public bool SearchInHistory
		{
			get => _searchInHistory;
			set => SetProperty(ref _searchInHistory, value);
		}

		private StringComparison _comparisonMode = StringComparison.CurrentCultureIgnoreCase;
		public StringComparison ComparisonMode
		{
			get => _comparisonMode;
			set => SetProperty(ref _comparisonMode, value);
		}

		private bool _matchDiacritics;
		public bool MatchDiacritics
		{
			get => _matchDiacritics;
			set => SetProperty(ref _matchDiacritics, value);
		}

		private bool _excludeExpired;
		public bool ExcludeExpired
		{
			get => _excludeExpired;
			set => SetProperty(ref _excludeExpired, value);
		}

		private bool _respectEntrySearchingDisabled = true;
		public bool RespectEntrySearchingDisabled
		{
			get => _respectEntrySearchingDisabled;
			set => SetProperty(ref _respectEntrySearchingDisabled, value);
		}

		// ------------------------------------------------------------------ //
		// Results                                                              //
		// ------------------------------------------------------------------ //

		public ObservableCollection<EntryProjection> SearchResults { get; } =
			new ObservableCollection<EntryProjection>();

		private int _resultCount;
		public int ResultCount
		{
			get => _resultCount;
			private set => SetProperty(ref _resultCount, value);
		}

		private bool _isSearchInProgress;
		public bool IsSearchInProgress
		{
			get => _isSearchInProgress;
			private set => SetProperty(ref _isSearchInProgress, value);
		}

		// ------------------------------------------------------------------ //
		// Profiles                                                             //
		// ------------------------------------------------------------------ //

		public ObservableCollection<SearchProfileDto> SearchProfiles { get; } =
			new ObservableCollection<SearchProfileDto>();

		private SearchProfileDto? _selectedProfile;
		public SearchProfileDto? SelectedProfile
		{
			get => _selectedProfile;
			set => SetProperty(ref _selectedProfile, value);
		}

		// ------------------------------------------------------------------ //
		// Commands                                                             //
		// ------------------------------------------------------------------ //

		public IAsyncRelayCommand SearchCommand { get; }
		public IRelayCommand SaveProfileCommand { get; }
		public IRelayCommand LoadProfileCommand { get; }
		public IRelayCommand DeleteProfileCommand { get; }

		// ------------------------------------------------------------------ //
		// Constructor                                                          //
		// ------------------------------------------------------------------ //

		/// <param name="rootGroupProvider">
		/// Factory that returns the root <see cref="PwGroup"/> to search.
		/// Called on the background thread during search execution.
		/// </param>
		/// <param name="mapper">Maps <see cref="PwEntry"/> results to projections.</param>
		/// <param name="profileStore">Persistence for named search profiles.</param>
		public SearchViewModel(
			Func<PwGroup> rootGroupProvider,
			EntryProjectionMapper mapper,
			ISearchProfileStore profileStore)
		{
			_rootGroupProvider = rootGroupProvider
				?? throw new ArgumentNullException(nameof(rootGroupProvider));
			_mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
			_profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));

			SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync, CanExecuteSearch);
			SaveProfileCommand = new RelayCommand<string>(ExecuteSaveProfile);
			LoadProfileCommand = new RelayCommand<SearchProfileDto>(ExecuteLoadProfile);
			DeleteProfileCommand = new RelayCommand<SearchProfileDto>(ExecuteDeleteProfile);

			LoadProfilesFromStore();
		}

		// ------------------------------------------------------------------ //
		// Search execution                                                     //
		// ------------------------------------------------------------------ //

		private bool CanExecuteSearch() =>
			!string.IsNullOrWhiteSpace(_searchString) && !_isSearchInProgress;

		private async Task ExecuteSearchAsync()
		{
			IsSearchInProgress = true;
			SearchCommand.NotifyCanExecuteChanged();

			SearchParameters sp = BuildSearchParameters();
			PwGroup root = _rootGroupProvider();

			List<EntryProjection> results;
			try
			{
				results = await Task.Run(() => RunSearch(sp, root));
			}
			finally
			{
				IsSearchInProgress = false;
				SearchCommand.NotifyCanExecuteChanged();
			}

			SearchResults.Clear();
			foreach (var ep in results) SearchResults.Add(ep);
			ResultCount = SearchResults.Count;
		}

		private List<EntryProjection> RunSearch(SearchParameters sp, PwGroup root)
		{
			var resultGroup = new PwGroup(true, true);
			root.SearchEntries(sp, resultGroup.Entries, new NullStatusLogger());

			var projections = new List<EntryProjection>((int)resultGroup.Entries.UCount);
			foreach (var entry in resultGroup.Entries)
				projections.Add(_mapper.FromDomain(entry));
			return projections;
		}

		// ------------------------------------------------------------------ //
		// Profile management                                                   //
		// ------------------------------------------------------------------ //

		private void ExecuteSaveProfile(string? profileName)
		{
			if (string.IsNullOrWhiteSpace(profileName)) return;

			SearchParameters sp = BuildSearchParameters();
			sp.Name = profileName!;

			_profileStore.SaveProfile(sp);

			// Remove existing entry with the same name, then add.
			for (int i = SearchProfiles.Count - 1; i >= 0; i--)
			{
				if (string.Equals(SearchProfiles[i].Name, profileName,
					StringComparison.OrdinalIgnoreCase))
					SearchProfiles.RemoveAt(i);
			}

			SearchProfiles.Add(new SearchProfileDto(sp));
		}

		private void ExecuteLoadProfile(SearchProfileDto? dto)
		{
			if (dto == null) return;
			ApplySearchParameters(dto.Parameters);
			SelectedProfile = dto;
		}

		private void ExecuteDeleteProfile(SearchProfileDto? dto)
		{
			if (dto == null) return;

			_profileStore.DeleteProfile(dto.Name);
			SearchProfiles.Remove(dto);

			if (ReferenceEquals(_selectedProfile, dto))
				SelectedProfile = null;
		}

		private void LoadProfilesFromStore()
		{
			foreach (var sp in _profileStore.GetProfiles())
				SearchProfiles.Add(new SearchProfileDto(sp));
		}

		// ------------------------------------------------------------------ //
		// SearchParameters ↔ ViewModel conversion                            //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// Constructs a <see cref="SearchParameters"/> instance reflecting the
		/// current ViewModel property values.
		/// </summary>
		public SearchParameters BuildSearchParameters()
		{
			return new SearchParameters
			{
				SearchString = _searchString,
				SearchMode = _searchMode,
				SearchInTitles = _searchInTitles,
				SearchInUserNames = _searchInUserNames,
				SearchInPasswords = _searchInPasswords,
				SearchInUrls = _searchInUrls,
				SearchInNotes = _searchInNotes,
				SearchInOther = _searchInOther,
				SearchInStringNames = _searchInStringNames,
				SearchInTags = _searchInTags,
				SearchInUuids = _searchInUuids,
				SearchInGroupPaths = _searchInGroupPaths,
				SearchInGroupNames = _searchInGroupNames,
				SearchInHistory = _searchInHistory,
				ComparisonMode = _comparisonMode,
				MatchDiacritics = _matchDiacritics,
				ExcludeExpired = _excludeExpired,
				RespectEntrySearchingDisabled = _respectEntrySearchingDisabled
			};
		}

		/// <summary>
		/// Populates ViewModel properties from an existing <see cref="SearchParameters"/>
		/// instance (e.g. when loading a saved profile).
		/// </summary>
		public void ApplySearchParameters(SearchParameters sp)
		{
			if (sp == null) throw new ArgumentNullException(nameof(sp));

			SearchString = sp.SearchString;
			SearchMode = sp.SearchMode;
			SearchInTitles = sp.SearchInTitles;
			SearchInUserNames = sp.SearchInUserNames;
			SearchInPasswords = sp.SearchInPasswords;
			SearchInUrls = sp.SearchInUrls;
			SearchInNotes = sp.SearchInNotes;
			SearchInOther = sp.SearchInOther;
			SearchInStringNames = sp.SearchInStringNames;
			SearchInTags = sp.SearchInTags;
			SearchInUuids = sp.SearchInUuids;
			SearchInGroupPaths = sp.SearchInGroupPaths;
			SearchInGroupNames = sp.SearchInGroupNames;
			SearchInHistory = sp.SearchInHistory;
			ComparisonMode = sp.ComparisonMode;
			MatchDiacritics = sp.MatchDiacritics;
			ExcludeExpired = sp.ExcludeExpired;
			RespectEntrySearchingDisabled = sp.RespectEntrySearchingDisabled;
		}
	}
}
