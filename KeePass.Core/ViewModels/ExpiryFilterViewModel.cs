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
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePass.Core.Projections;

using KeePassLib;
using KeePassLib.Collections;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Predefined time windows for expiry-based entry filtering.
	/// </summary>
	public sealed class ExpiryPeriod
	{
		/// <summary>Human-readable display label.</summary>
		public string Label { get; }

		/// <summary>
		/// Number of days from today to include.
		/// <c>null</c> means show already-expired entries only.
		/// </summary>
		public int? DaysAhead { get; }

		public ExpiryPeriod(string label, int? daysAhead)
		{
			Label     = label;
			DaysAhead = daysAhead;
		}

		public override string ToString() => Label;
	}

	/// <summary>
	/// View-model for expiry-based entry filtering.
	///
	/// <para>
	/// Provides predefined periods (already expired, next 7/30 days) and a
	/// custom date range. Results are projected through the entry mapper and
	/// emitted via <see cref="ResultsReady"/>.
	/// </para>
	/// </summary>
	public sealed class ExpiryFilterViewModel : ObservableObject
	{
		private readonly IProjectionMapper<PwEntry, EntryProjection> _entryMapper;
		private PwGroup? _rootGroup;

		private ExpiryPeriod? _selectedPeriod;
		private bool _isFiltering;

		// ── Events ────────────────────────────────────────────────────────────

		/// <summary>Raised when the filtered results are ready.</summary>
		public event EventHandler<IReadOnlyList<EntryProjection>>? ResultsReady;

		// ── Properties ───────────────────────────────────────────────────────

		/// <summary>Available time-period options.</summary>
		public ObservableCollection<ExpiryPeriod> Periods { get; }

		/// <summary>The selected expiry period, or <c>null</c> for no filter.</summary>
		public ExpiryPeriod? SelectedPeriod
		{
			get => _selectedPeriod;
			set
			{
				if (SetProperty(ref _selectedPeriod, value))
					FilterCommand.NotifyCanExecuteChanged();
			}
		}

		/// <summary>Whether a filter operation is in progress.</summary>
		public bool IsFiltering
		{
			get => _isFiltering;
			private set => SetProperty(ref _isFiltering, value);
		}

		// ── Commands ─────────────────────────────────────────────────────────

		/// <summary>Runs the selected expiry filter.</summary>
		public IAsyncRelayCommand FilterCommand { get; }

		/// <summary>Clears the expiry filter.</summary>
		public IRelayCommand ClearCommand { get; }

		// ── Construction ─────────────────────────────────────────────────────

		public ExpiryFilterViewModel(IProjectionMapper<PwEntry, EntryProjection> entryMapper)
		{
			_entryMapper = entryMapper ?? throw new ArgumentNullException(nameof(entryMapper));

			Periods = new ObservableCollection<ExpiryPeriod>
			{
				new ExpiryPeriod("Already expired",  null),
				new ExpiryPeriod("Expires in 7 days",   7),
				new ExpiryPeriod("Expires in 30 days",  30),
				new ExpiryPeriod("Expires in 90 days",  90),
			};

			FilterCommand = new AsyncRelayCommand(ExecuteFilterAsync, () => _selectedPeriod != null);
			ClearCommand  = new RelayCommand(ExecuteClear);
		}

		// ── Public API ───────────────────────────────────────────────────────

		/// <summary>Sets the group tree to filter against.</summary>
		public void LoadGroup(PwGroup? rootGroup) => _rootGroup = rootGroup;

		// ── Command implementations ───────────────────────────────────────────

		private async Task ExecuteFilterAsync()
		{
			if (_rootGroup == null || _selectedPeriod == null) return;

			IsFiltering = true;
			int? daysAhead = _selectedPeriod.DaysAhead;
			DateTime now   = DateTime.UtcNow;
			PwGroup root   = _rootGroup;
			var mapper     = _entryMapper;

			try
			{
				var results = await Task.Run(() =>
				{
					var matches = new List<EntryProjection>();

					root.TraverseTree(TraversalMethod.PreOrder, null, entry =>
					{
						if (!entry.Expires) return true;

						bool include = daysAhead.HasValue
							? entry.ExpiryTime <= now.AddDays(daysAhead.Value)
							: entry.ExpiryTime <= now;

						if (include)
							matches.Add(mapper.FromDomain(entry));

						return true;
					});

					return matches;
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
			SelectedPeriod = null;
			ResultsReady?.Invoke(this, Array.Empty<EntryProjection>());
		}
	}
}
