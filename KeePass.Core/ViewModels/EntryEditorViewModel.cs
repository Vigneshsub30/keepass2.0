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

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Security;
using KeePassLib.Utility;

using KeePass.Core.Projections;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for the entry editor dialog.
	///
	/// <para>
	/// Operates on a <em>working copy</em> of field values; the source
	/// <see cref="PwEntry"/> is not modified until <see cref="SaveCommand"/>
	/// is executed.  <see cref="CancelCommand"/> discards all edits without
	/// touching the source entry.
	/// </para>
	/// <para>
	/// Has zero references to <c>System.Windows.Forms</c> or
	/// <c>System.Drawing</c> and can be fully unit-tested in isolation.
	/// </para>
	/// </summary>
	public sealed class EntryEditorViewModel : ObservableValidator
	{
		private readonly PwEntry _source;
		private readonly EntryProjectionMapper _mapper;
		private bool _isCreateMode;

		// ── Standard field backing fields ─────────────────────────────────────

		private string _title;

		private string _userName;
		private ProtectedString _password;
		private ProtectedString _passwordRepeat;
		private string _url;
		private string _notes;
		private PwIcon _iconId;
		private PwUuid _customIconUuid;
		private DateTime _expiryTime;
		private bool _expires;
		private string _foregroundColorHex;
		private string _backgroundColorHex;
		private bool _qualityCheck;

		// ── Events ────────────────────────────────────────────────────────────

		/// <summary>Raised after <see cref="SaveCommand"/> successfully applies changes.</summary>
		public event EventHandler Saved;

		/// <summary>Raised after <see cref="CancelCommand"/> discards changes.</summary>
		public event EventHandler Cancelled;

		// ── Construction ─────────────────────────────────────────────────────

		/// <summary>
		/// Creates an editor initialised from <paramref name="source"/> (edit mode).
		/// </summary>
		public EntryEditorViewModel(PwEntry source)
			: this(source, new EntryProjectionMapper()) { }

		/// <summary>
		/// Creates an editor using the supplied mapper (for testability).
		/// </summary>
		public EntryEditorViewModel(PwEntry source, EntryProjectionMapper mapper)
		{
			if(mapper == null) throw new ArgumentNullException("mapper");

			_mapper      = mapper;
			_source      = source;
			_isCreateMode = (source == null);

			if(_isCreateMode)
				PopulateDefaults();
			else
				PopulateFromEntry(source);

			// Commands
			SaveCommand   = new RelayCommand(Save,   CanSave);
			CancelCommand = new RelayCommand(Cancel);

			AddFieldCommand    = new RelayCommand<string>(AddField);
			RemoveFieldCommand = new RelayCommand<FieldViewModel>(RemoveField,
				fvm => fvm != null);

			AddAttachmentCommand    = new RelayCommand<AttachmentData>(AddAttachment,
				a => a != null);
			RemoveAttachmentCommand = new RelayCommand<BinaryReference>(RemoveAttachment,
				b => b != null);

			AddAssociationCommand    = new RelayCommand(AddAssociation);
			RemoveAssociationCommand = new RelayCommand<AutoTypeAssociationViewModel>(
				RemoveAssociation, a => a != null);
		}

		// ── Standard observable properties ────────────────────────────────────

		/// <summary>The entry title (required, must not be empty).</summary>
		public string Title
		{
			get { return _title; }
			set
			{
				SetProperty(ref _title, value, true /* validate */);
				SaveCommand.NotifyCanExecuteChanged();
				OnPropertyChanged("HasErrors");
			}
		}

		/// <summary>The user name for this entry.</summary>
		public string UserName
		{
			get { return _userName; }
			set { SetProperty(ref _userName, value); }
		}

		/// <summary>The password (protected).</summary>
		public ProtectedString Password
		{
			get { return _password; }
			set
			{
				SetProperty(ref _password, value);
				OnPropertyChanged("PasswordQualityBits");
				ValidatePasswordMatch();
			}
		}

		/// <summary>
		/// Password repeat for confirmation (protected).
		/// Must equal <see cref="Password"/> when both are non-empty.
		/// </summary>
		public ProtectedString PasswordRepeat
		{
			get { return _passwordRepeat; }
			set
			{
				SetProperty(ref _passwordRepeat, value);
				ValidatePasswordMatch();
				SaveCommand.NotifyCanExecuteChanged();
			}
		}

		/// <summary>The URL for this entry.</summary>
		public string Url
		{
			get { return _url; }
			set { SetProperty(ref _url, value); }
		}

		/// <summary>Multi-line notes.</summary>
		public string Notes
		{
			get { return _notes; }
			set { SetProperty(ref _notes, value); }
		}

		/// <summary>The built-in icon identifier.</summary>
		public PwIcon IconId
		{
			get { return _iconId; }
			set { SetProperty(ref _iconId, value); }
		}

		/// <summary>Custom icon UUID, or <see cref="PwUuid.Zero"/> for none.</summary>
		public PwUuid CustomIconUuid
		{
			get { return _customIconUuid; }
			set { SetProperty(ref _customIconUuid, value); }
		}

		/// <summary>When the entry expires (UTC).</summary>
		public DateTime ExpiryTime
		{
			get { return _expiryTime; }
			set { SetProperty(ref _expiryTime, value); }
		}

		/// <summary>Whether the entry has an expiry date.</summary>
		public bool Expires
		{
			get { return _expires; }
			set { SetProperty(ref _expires, value); }
		}

		/// <summary>Foreground colour as 6-digit hex (<c>"RRGGBB"</c>) or <c>null</c>.</summary>
		public string ForegroundColorHex
		{
			get { return _foregroundColorHex; }
			set { SetProperty(ref _foregroundColorHex, value); }
		}

		/// <summary>Background colour as 6-digit hex (<c>"RRGGBB"</c>) or <c>null</c>.</summary>
		public string BackgroundColorHex
		{
			get { return _backgroundColorHex; }
			set { SetProperty(ref _backgroundColorHex, value); }
		}

		/// <summary>Whether the password quality checker should run on this entry.</summary>
		public bool QualityCheck
		{
			get { return _qualityCheck; }
			set { SetProperty(ref _qualityCheck, value); }
		}

		/// <summary>Mutable list of the entry's tags.</summary>
		public ObservableCollection<string> Tags { get; private set; }

		// ── Collections ───────────────────────────────────────────────────────

		/// <summary>Custom (non-standard) string fields for editing.</summary>
		public ObservableCollection<FieldViewModel> CustomFields { get; private set; }

		/// <summary>Binary attachments as descriptive references.</summary>
		public ObservableCollection<BinaryReference> Attachments { get; private set; }

		/// <summary>Auto-type window/sequence associations.</summary>
		public ObservableCollection<AutoTypeAssociationViewModel> AutoTypeAssociations { get; private set; }

		/// <summary>Read-only history summaries (displayed but not editable).</summary>
		public IReadOnlyList<EntryHistorySummary> HistoryEntries { get; private set; }

		// ── Password match tracking (cross-field validation) ─────────────────

		private bool _passwordMatchError;

		/// <summary><c>true</c> when the two password fields do not match.</summary>
		public bool PasswordMismatch
		{
			get { return _passwordMatchError; }
			private set { SetProperty(ref _passwordMatchError, value); }
		}

		// ── Computed ─────────────────────────────────────────────────────────

		/// <summary>
		/// Estimated quality of the current <see cref="Password"/> in bits.
		/// Returns 0 when the password is null or empty.
		/// </summary>
		public uint PasswordQualityBits
		{
			get
			{
				if(_password == null) return 0u;
				byte[] data = _password.ReadUtf8();
				if(data == null || data.Length == 0) return 0u;
				uint bits = QualityEstimation.EstimatePasswordBits(data);
				MemUtil.ZeroByteArray(data);
				return bits;
			}
		}

		/// <summary>Whether the current state has no validation errors.</summary>
		public bool HasErrors { get { return base.HasErrors; } }

		/// <summary>Whether the ViewModel was initialised with no source entry (create mode).</summary>
		public bool IsCreateMode { get { return _isCreateMode; } }

		// ── Commands ─────────────────────────────────────────────────────────

		/// <summary>Saves changes to the source <see cref="PwEntry"/>.</summary>
		public IRelayCommand SaveCommand   { get; }

		/// <summary>Discards all changes without modifying the source entry.</summary>
		public IRelayCommand CancelCommand { get; }

		/// <summary>Adds a new custom field with the given name.</summary>
		public IRelayCommand<string> AddFieldCommand { get; }

		/// <summary>Removes the specified custom field.</summary>
		public IRelayCommand<FieldViewModel> RemoveFieldCommand { get; }

		/// <summary>Adds a binary attachment from the supplied data.</summary>
		public IRelayCommand<AttachmentData> AddAttachmentCommand { get; }

		/// <summary>Removes the specified binary attachment.</summary>
		public IRelayCommand<BinaryReference> RemoveAttachmentCommand { get; }

		/// <summary>Adds a new (empty) auto-type association.</summary>
		public IRelayCommand AddAssociationCommand { get; }

		/// <summary>Removes the specified auto-type association.</summary>
		public IRelayCommand<AutoTypeAssociationViewModel> RemoveAssociationCommand { get; }

		// ── Private command implementations ───────────────────────────────────

		private bool CanSave()
		{
			return !string.IsNullOrWhiteSpace(_title) && !PasswordMismatch;
		}

		private void Save()
		{
			if(!CanSave()) return;

			PwEntry target;
			if(_isCreateMode)
			{
				target = new PwEntry(true, true);
			}
			else
			{
				// Create a history backup before mutating
				_source.CreateBackup(null);
				target = _source;
			}

			ApplyToEntry(target);
			target.Touch(true);

			Saved?.Invoke(this, EventArgs.Empty);
		}

		private void Cancel()
		{
			Cancelled?.Invoke(this, EventArgs.Empty);
		}

		private void AddField(string name)
		{
			string fieldName = string.IsNullOrEmpty(name)
				? "Field " + (CustomFields.Count + 1)
				: name;
			CustomFields.Add(new FieldViewModel(fieldName, new ProtectedString(false, string.Empty)));
		}

		private void RemoveField(FieldViewModel fvm)
		{
			CustomFields.Remove(fvm);
		}

		private void AddAttachment(AttachmentData data)
		{
			if(data == null || string.IsNullOrEmpty(data.Name)) return;

			// Compute SHA-256 hash for the reference
			string hash;
			using(var sha = System.Security.Cryptography.SHA256.Create())
			{
				byte[] h = sha.ComputeHash(data.Content ?? new byte[0]);
				var sb = new System.Text.StringBuilder(h.Length * 2);
				foreach(byte b in h)
					sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
				hash = sb.ToString();
			}

			Attachments.Add(new BinaryReference
			{
				Name        = data.Name,
				Size        = data.Content != null ? data.Content.LongLength : 0L,
				ContentHash = hash,
			});
		}

		private void RemoveAttachment(BinaryReference binRef)
		{
			Attachments.Remove(binRef);
		}

		private void AddAssociation()
		{
			AutoTypeAssociations.Add(new AutoTypeAssociationViewModel(string.Empty, string.Empty));
		}

		private void RemoveAssociation(AutoTypeAssociationViewModel assoc)
		{
			AutoTypeAssociations.Remove(assoc);
		}

		// ── Population helpers ────────────────────────────────────────────────

		private void PopulateDefaults()
		{
			_title           = string.Empty;
			_userName        = string.Empty;
			_password        = new ProtectedString(true, string.Empty);
			_passwordRepeat  = new ProtectedString(true, string.Empty);
			_url             = string.Empty;
			_notes           = string.Empty;
			_iconId          = PwIcon.Key;
			_customIconUuid  = PwUuid.Zero;
			_expiryTime      = DateTime.UtcNow.AddYears(1);
			_expires         = false;
			_qualityCheck    = true;

			Tags                 = new ObservableCollection<string>();
			CustomFields         = new ObservableCollection<FieldViewModel>();
			Attachments          = new ObservableCollection<BinaryReference>();
			AutoTypeAssociations = new ObservableCollection<AutoTypeAssociationViewModel>();
			HistoryEntries       = new List<EntryHistorySummary>();
		}

		private void PopulateFromEntry(PwEntry e)
		{
			_title           = e.Strings.ReadSafe(PwDefs.TitleField);
			_userName        = e.Strings.ReadSafe(PwDefs.UserNameField);
			_password        = e.Strings.GetSafe(PwDefs.PasswordField);
			_passwordRepeat  = e.Strings.GetSafe(PwDefs.PasswordField); // pre-fill repeat
			_url             = e.Strings.ReadSafe(PwDefs.UrlField);
			_notes           = e.Strings.ReadSafe(PwDefs.NotesField);
			_iconId          = e.IconId;
			_customIconUuid  = e.CustomIconUuid;
			_expiryTime      = e.ExpiryTime;
			_expires         = e.Expires;
			_qualityCheck    = e.QualityCheck;
			// Colors are not available in net10.0/UAP builds; always null
			_foregroundColorHex = null;
			_backgroundColorHex = null;

			Tags = new ObservableCollection<string>(e.Tags ?? new List<string>());

			var customFields = new ObservableCollection<FieldViewModel>();
			foreach(KeyValuePair<string, ProtectedString> kv in e.Strings)
			{
				if(!PwDefs.IsStandardField(kv.Key))
					customFields.Add(new FieldViewModel(kv.Key, kv.Value));
			}
			CustomFields = customFields;

			var attachments = new ObservableCollection<BinaryReference>();
			foreach(KeyValuePair<string, ProtectedBinary> kv in e.Binaries)
			{
				byte[] data = kv.Value.ReadData();
				string hash = ComputeHex(System.Security.Cryptography.SHA256.Create()
					.ComputeHash(data ?? new byte[0]));
				attachments.Add(new BinaryReference
				{
					Name        = kv.Key,
					Size        = data != null ? data.LongLength : 0L,
					ContentHash = hash,
				});
				if(data != null) MemUtil.ZeroByteArray(data);
			}
			Attachments = attachments;

			var associations = new ObservableCollection<AutoTypeAssociationViewModel>();
			for(int i = 0; i < e.AutoType.AssociationsCount; i++)
			{
				AutoTypeAssociation a = e.AutoType.GetAt(i);
				associations.Add(new AutoTypeAssociationViewModel(a.WindowName, a.Sequence));
			}
			AutoTypeAssociations = associations;

			// History summaries (read-only display)
			var histList = new List<EntryHistorySummary>((int)e.History.UCount);
			for(uint i = 0; i < e.History.UCount; i++)
			{
				PwEntry h = e.History.GetAt(i);
				histList.Add(new EntryHistorySummary
				{
					Uuid                 = h.Uuid,
					LastModificationTime = h.LastModificationTime,
					Title                = h.Strings.ReadSafe(PwDefs.TitleField),
				});
			}
			HistoryEntries = histList;
		}

		private void ApplyToEntry(PwEntry target)
		{
			target.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, _title    ?? string.Empty));
			target.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, _userName ?? string.Empty));
			target.Strings.Set(PwDefs.PasswordField, _password ?? ProtectedString.Empty);
			target.Strings.Set(PwDefs.UrlField,      new ProtectedString(false, _url     ?? string.Empty));
			target.Strings.Set(PwDefs.NotesField,    new ProtectedString(false, _notes   ?? string.Empty));

			target.IconId       = _iconId;
			target.CustomIconUuid = _customIconUuid;
			target.ExpiryTime   = _expiryTime;
			target.Expires      = _expires;
			target.QualityCheck = _qualityCheck;

			// Custom fields: clear non-standard, then re-add from ViewModel
			List<string> toRemove = new List<string>();
			foreach(KeyValuePair<string, ProtectedString> kv in target.Strings)
				if(!PwDefs.IsStandardField(kv.Key)) toRemove.Add(kv.Key);
			foreach(string key in toRemove)
				target.Strings.Remove(key);
			foreach(FieldViewModel fvm in CustomFields)
				if(!string.IsNullOrEmpty(fvm.Name))
					target.Strings.Set(fvm.Name, fvm.Value ?? new ProtectedString(false, string.Empty));

			// Tags
			target.Tags = new List<string>(Tags);

			// Auto-type associations
			target.AutoType.Clear();
			foreach(AutoTypeAssociationViewModel avm in AutoTypeAssociations)
				target.AutoType.Add(new AutoTypeAssociation(avm.WindowName, avm.Sequence));
		}

		private void ValidatePasswordMatch()
		{
			PasswordMismatch = !PasswordsMatch();
			OnPropertyChanged("HasErrors");
			SaveCommand?.NotifyCanExecuteChanged();
		}

		private bool PasswordsMatch()
		{
			string p1 = _password != null ? _password.ReadString() : string.Empty;
			string p2 = _passwordRepeat != null ? _passwordRepeat.ReadString() : string.Empty;
			return p1 == p2;
		}

		private static string ComputeHex(byte[] data)
		{
			var sb = new System.Text.StringBuilder(data.Length * 2);
			foreach(byte b in data)
				sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
			return sb.ToString();
		}
	}

	/// <summary>Carries a filename and raw bytes for a new binary attachment.</summary>
	public sealed class AttachmentData
	{
		public string Name    { get; set; }
		public byte[] Content { get; set; }
	}
}
