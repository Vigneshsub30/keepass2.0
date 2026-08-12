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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePassLib;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Represents a single item (icon) in the <see cref="IconPickerViewModel"/> grid.
	/// </summary>
	public sealed class IconItemViewModel : ObservableObject
	{
		private bool _isSelected;

		/// <summary>The <see cref="PwIcon"/> value this item represents.</summary>
		public PwIcon IconId { get; }

		/// <summary>Human-readable label for the icon.</summary>
		public string Label { get; }

		/// <summary>Whether this icon is currently selected in the picker.</summary>
		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		public IconItemViewModel(PwIcon iconId)
		{
			IconId = iconId;
			Label  = iconId.ToString();
		}
	}

	/// <summary>
	/// View-model for the icon picker dialog.
	///
	/// <para>
	/// Exposes all 69 standard <see cref="PwIcon"/> values as a flat observable
	/// collection that drives a grid of selectable buttons.  The caller reads
	/// <see cref="SelectedIconId"/> after the dialog is confirmed.
	/// </para>
	/// </summary>
	public sealed class IconPickerViewModel : ObservableObject
	{
		private PwIcon _selectedIconId;

		// ── Events ────────────────────────────────────────────────────────────

		/// <summary>Raised when the user confirms the selection.</summary>
		public event EventHandler? SelectionConfirmed;

		/// <summary>Raised when the user cancels without selecting.</summary>
		public event EventHandler? SelectionCancelled;

		// ── Properties ───────────────────────────────────────────────────────

		/// <summary>All available standard icons.</summary>
		public ObservableCollection<IconItemViewModel> Icons { get; }
			= new ObservableCollection<IconItemViewModel>();

		/// <summary>The currently selected icon index.</summary>
		public PwIcon SelectedIconId
		{
			get => _selectedIconId;
			set
			{
				if (SetProperty(ref _selectedIconId, value))
					SyncSelection();
			}
		}

		// ── Commands ─────────────────────────────────────────────────────────

		/// <summary>Selects an icon by clicking its button.</summary>
		public IRelayCommand<IconItemViewModel> SelectIconCommand { get; }

		/// <summary>Confirms the current selection and closes the picker.</summary>
		public IRelayCommand ConfirmCommand { get; }

		/// <summary>Cancels selection and closes the picker.</summary>
		public IRelayCommand CancelCommand { get; }

		// ── Construction ─────────────────────────────────────────────────────

		/// <param name="currentIconId">The icon that should be pre-selected.</param>
		public IconPickerViewModel(PwIcon currentIconId = PwIcon.Key)
		{
			_selectedIconId = currentIconId;

			foreach (PwIcon icon in Enum.GetValues(typeof(PwIcon)))
			{
				if ((int)icon >= 0 && (int)icon < (int)PwIcon.Count)
					Icons.Add(new IconItemViewModel(icon));
			}

			SyncSelection();

			SelectIconCommand = new RelayCommand<IconItemViewModel>(SelectIcon);
			ConfirmCommand    = new RelayCommand(() => SelectionConfirmed?.Invoke(this, EventArgs.Empty));
			CancelCommand     = new RelayCommand(() => SelectionCancelled?.Invoke(this, EventArgs.Empty));
		}

		// ── Private ──────────────────────────────────────────────────────────

		private void SelectIcon(IconItemViewModel? item)
		{
			if (item == null) return;

			foreach (var icon in Icons)
				icon.IsSelected = false;

			item.IsSelected = true;
			SetProperty(ref _selectedIconId, item.IconId, nameof(SelectedIconId));
		}

		private void SyncSelection()
		{
			foreach (var icon in Icons)
				icon.IsSelected = (icon.IconId == _selectedIconId);
		}
	}
}
