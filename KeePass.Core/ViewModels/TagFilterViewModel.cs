/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePass.Core.Projections;

using KeePassLib;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for tag-based entry filtering.
	///
	/// <para>
	/// Exposes a flat list of all tags present in a <see cref="PwGroup"/> tree
	/// and filters the entry list to entries matching the selected tag.
	/// </para>
	/// </summary>
	public sealed class TagFilterViewModel : ObservableObject
	{
		private readonly IProjectionMapper<PwEntry, EntryProjection> _entryMapper;
		private PwGroup? _rootGroup;

		private string? _selectedTag;
		private bool _isFiltering;

		// ── Events ────────────────────────────────────────────────────────────

		/// <summary>Raised when the filtered results are ready.</summary>
		public event EventHandler<System.Collections.Generic.IReadOnlyList<EntryProjection>>?
			ResultsReady;

		// ── Properties ───────────────────────────────────────────────────────

		/// <summary>All tags found in the currently loaded group tree.</summary>
		public ObservableCollection<string> AvailableTags { get; }
			= new ObservableCollection<string>();

		/// <summary>The currently selected tag, or <c>null</c> for no filter.</summary>
		public string? SelectedTag
		{
			get => _selectedTag;
			set
			{
				if (SetProperty(ref _selectedTag, value))
					FilterCommand.NotifyCanExecuteChanged();
			}
		}

		/// <summary>Whether a filter operation is currently running.</summary>
		public bool IsFiltering
		{
			get => _isFiltering;
			private set => SetProperty(ref _isFiltering, value);
		}

		// ── Commands ─────────────────────────────────────────────────────────

		/// <summary>Applies the selected tag filter.</summary>
		public IAsyncRelayCommand FilterCommand { get; }

		/// <summary>Clears the current filter.</summary>
		public IRelayCommand ClearCommand { get; }

		// ── Construction ─────────────────────────────────────────────────────

		public TagFilterViewModel(IProjectionMapper<PwEntry, EntryProjection> entryMapper)
		{
			_entryMapper = entryMapper ?? throw new ArgumentNullException(nameof(entryMapper));

			FilterCommand = new AsyncRelayCommand(ExecuteFilterAsync, () => _selectedTag != null);
			ClearCommand  = new RelayCommand(ExecuteClear);
		}

		// ── Public API ───────────────────────────────────────────────────────

		/// <summary>
		/// Loads available tags from the supplied group tree.
		/// Call this whenever the active database changes.
		/// </summary>
		public void LoadTags(PwGroup? rootGroup)
		{
			_rootGroup = rootGroup;
			AvailableTags.Clear();

			if (rootGroup == null) return;

			foreach (string tag in rootGroup.BuildEntryTagsList(true))
				AvailableTags.Add(tag);
		}

		// ── Command implementations ───────────────────────────────────────────

		private async Task ExecuteFilterAsync()
		{
			if (_rootGroup == null || _selectedTag == null) return;

			IsFiltering = true;
			string tag = _selectedTag;
			PwGroup root = _rootGroup;
			var mapper = _entryMapper;

			try
			{
				var results = await Task.Run(() =>
				{
					var matches = new KeePassLib.Collections.PwObjectList<PwEntry>();
					root.FindEntriesByTag(tag, matches, true);

					var projected = new System.Collections.Generic.List<EntryProjection>(
						(int)matches.UCount);
					for (uint i = 0; i < matches.UCount; i++)
						projected.Add(mapper.FromDomain(matches.GetAt(i)));
					return projected;
				});

				ResultsReady?.Invoke(this, results);
			}
			finally
			{
				IsFiltering = false;
			}
		}

		private void ExecuteClear()
		{
			SelectedTag = null;
			ResultsReady?.Invoke(this,
				System.Array.Empty<EntryProjection>());
		}
	}
}
