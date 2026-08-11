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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePass.Core.Models;

using KeePassLib;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for the group editor dialog.
	///
	/// <para>
	/// Operates on a working copy of the group's fields; the source
	/// <see cref="PwGroup"/> is not modified until <see cref="SaveCommand"/>
	/// is executed.
	/// </para>
	/// </summary>
	public sealed class GroupEditorViewModel : ObservableObject
	{
		private readonly PwGroup? _source;

		// ── Backing fields ────────────────────────────────────────────────────

		private string _name = string.Empty;
		private string _notes = string.Empty;
		private PwIcon _iconId = PwIcon.Folder;
		private PwUuid _customIconUuid = PwUuid.Zero;
		private string _defaultAutoTypeSequence = string.Empty;
		private InheritableBoolean _enableAutoType = InheritableBoolean.Inherit;
		private InheritableBoolean _enableSearching = InheritableBoolean.Inherit;
		private bool _expires;
		private DateTime _expiryTime = DateTime.UtcNow.AddYears(1);

		// ── Events ────────────────────────────────────────────────────────────

		/// <summary>Raised after <see cref="SaveCommand"/> successfully applies changes.</summary>
		public event EventHandler? Saved;

		/// <summary>Raised after <see cref="CancelCommand"/> discards changes.</summary>
		public event EventHandler? Cancelled;

		// ── Construction ─────────────────────────────────────────────────────

		/// <summary>
		/// Creates a view-model in create mode (no source group).
		/// </summary>
		public GroupEditorViewModel() : this(null) { }

		/// <summary>
		/// Creates a view-model pre-populated from <paramref name="source"/>.
		/// </summary>
		public GroupEditorViewModel(PwGroup? source)
		{
			_source = source;

			if (source != null)
				PopulateFromGroup(source);

			SaveCommand   = new RelayCommand(Save, CanSave);
			CancelCommand = new RelayCommand(Cancel);
		}

		// ── Observable properties ─────────────────────────────────────────────

		/// <summary>The group name (required).</summary>
		public string Name
		{
			get => _name;
			set
			{
				SetProperty(ref _name, value ?? string.Empty);
				SaveCommand.NotifyCanExecuteChanged();
			}
		}

		/// <summary>Group notes (free-form text).</summary>
		public string Notes
		{
			get => _notes;
			set => SetProperty(ref _notes, value ?? string.Empty);
		}

		/// <summary>Standard icon index for this group.</summary>
		public PwIcon IconId
		{
			get => _iconId;
			set => SetProperty(ref _iconId, value);
		}

		/// <summary>Custom icon UUID, or <see cref="PwUuid.Zero"/> for none.</summary>
		public PwUuid CustomIconUuid
		{
			get => _customIconUuid;
			set => SetProperty(ref _customIconUuid, value);
		}

		/// <summary>Default auto-type key sequence for child entries.</summary>
		public string DefaultAutoTypeSequence
		{
			get => _defaultAutoTypeSequence;
			set => SetProperty(ref _defaultAutoTypeSequence, value ?? string.Empty);
		}

		/// <summary>Three-state auto-type enablement (inherit/enabled/disabled).</summary>
		public InheritableBoolean EnableAutoType
		{
			get => _enableAutoType;
			set => SetProperty(ref _enableAutoType, value);
		}

		/// <summary>Three-state search enablement (inherit/enabled/disabled).</summary>
		public InheritableBoolean EnableSearching
		{
			get => _enableSearching;
			set => SetProperty(ref _enableSearching, value);
		}

		/// <summary>Whether the group has an expiry date.</summary>
		public bool Expires
		{
			get => _expires;
			set => SetProperty(ref _expires, value);
		}

		/// <summary>Group expiry time (UTC).</summary>
		public DateTime ExpiryTime
		{
			get => _expiryTime;
			set => SetProperty(ref _expiryTime, value);
		}

		/// <summary>Mutable list of the group's tags.</summary>
		public ObservableCollection<string> Tags { get; private set; }
			= new ObservableCollection<string>();

		/// <summary>Whether the ViewModel was created without a source group.</summary>
		public bool IsCreateMode => _source == null;

		// ── Commands ─────────────────────────────────────────────────────────

		/// <summary>Applies changes to the source group (or creates a new one).</summary>
		public IRelayCommand SaveCommand { get; }

		/// <summary>Discards all changes.</summary>
		public IRelayCommand CancelCommand { get; }

		// ── Command implementations ───────────────────────────────────────────

		private bool CanSave() => !string.IsNullOrWhiteSpace(_name);

		private void Save()
		{
			if (!CanSave()) return;

			PwGroup target = _source ?? new PwGroup(true, true);
			ApplyToGroup(target);

			Saved?.Invoke(this, EventArgs.Empty);
		}

		private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

		// ── Helpers ───────────────────────────────────────────────────────────

		private void PopulateFromGroup(PwGroup g)
		{
			_name                    = g.Name;
			_notes                   = g.Notes;
			_iconId                  = g.IconId;
			_customIconUuid          = g.CustomIconUuid;
			_defaultAutoTypeSequence = g.DefaultAutoTypeSequence;
			_enableAutoType          = InheritableBooleanExtensions.FromNullableBool(g.EnableAutoType);
			_enableSearching         = InheritableBooleanExtensions.FromNullableBool(g.EnableSearching);
			_expires                 = g.Expires;
			_expiryTime              = g.ExpiryTime;

			Tags = new ObservableCollection<string>(g.Tags ?? new List<string>());
		}

		private void ApplyToGroup(PwGroup target)
		{
			target.Name                    = _name;
			target.Notes                   = _notes;
			target.IconId                  = _iconId;
			target.CustomIconUuid          = _customIconUuid;
			target.DefaultAutoTypeSequence = _defaultAutoTypeSequence;
			target.EnableAutoType          = _enableAutoType.ToNullableBool();
			target.EnableSearching         = _enableSearching.ToNullableBool();
			target.Expires                 = _expires;
			target.ExpiryTime              = _expiryTime;
			target.Tags                    = new List<string>(Tags);
		}
	}
}
