using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePassLib;
using KeePassLib.Security;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Read-only preview of the currently selected entry. Exposes all standard
	/// fields and a subset of metadata. The password field is masked by default;
	/// toggling <see cref="IsPasswordVisible"/> reveals the plain text.
	/// </summary>
	public sealed class EntryPreviewViewModel : ObservableObject
	{
		// ------------------------------------------------------------------ //
		// Standard fields                                                      //
		// ------------------------------------------------------------------ //

		private string _title = string.Empty;
		public string Title
		{
			get => _title;
			private set => SetProperty(ref _title, value ?? string.Empty);
		}

		private string _userName = string.Empty;
		public string UserName
		{
			get => _userName;
			private set => SetProperty(ref _userName, value ?? string.Empty);
		}

		private string _password = string.Empty;
		private string _maskedPassword = string.Empty;

		public string Password => _isPasswordVisible ? _password : _maskedPassword;

		private string _url = string.Empty;
		public string Url
		{
			get => _url;
			private set => SetProperty(ref _url, value ?? string.Empty);
		}

		private string _notes = string.Empty;
		public string Notes
		{
			get => _notes;
			private set => SetProperty(ref _notes, value ?? string.Empty);
		}

		// ------------------------------------------------------------------ //
		// Password visibility toggle                                           //
		// ------------------------------------------------------------------ //

		private bool _isPasswordVisible;
		public bool IsPasswordVisible
		{
			get => _isPasswordVisible;
			private set
			{
				if (SetProperty(ref _isPasswordVisible, value))
					OnPropertyChanged(nameof(Password));
			}
		}

		public IRelayCommand TogglePasswordVisibilityCommand { get; }

		// ------------------------------------------------------------------ //
		// Metadata                                                             //
		// ------------------------------------------------------------------ //

		private DateTime _lastModified = DateTime.MinValue;
		public DateTime LastModified
		{
			get => _lastModified;
			private set => SetProperty(ref _lastModified, value);
		}

		private DateTime? _expiryTime;
		public DateTime? ExpiryTime
		{
			get => _expiryTime;
			private set => SetProperty(ref _expiryTime, value);
		}

		private bool _isExpired;
		public bool IsExpired
		{
			get => _isExpired;
			private set => SetProperty(ref _isExpired, value);
		}

		// ------------------------------------------------------------------ //
		// Custom fields                                                        //
		// ------------------------------------------------------------------ //

		public ObservableCollection<FieldViewModel> CustomFields { get; } =
			new ObservableCollection<FieldViewModel>();

		// ------------------------------------------------------------------ //
		// Empty-state indicator                                                //
		// ------------------------------------------------------------------ //

		private bool _isEmpty = true;
		public bool IsEmpty
		{
			get => _isEmpty;
			private set => SetProperty(ref _isEmpty, value);
		}

		// ------------------------------------------------------------------ //
		// Construction                                                         //
		// ------------------------------------------------------------------ //

		public EntryPreviewViewModel()
		{
			TogglePasswordVisibilityCommand =
				new RelayCommand(() => IsPasswordVisible = !_isPasswordVisible);
		}

		// ------------------------------------------------------------------ //
		// Load / Clear                                                         //
		// ------------------------------------------------------------------ //

		/// <summary>Populates this VM from a selected entry.</summary>
		public void LoadEntry(PwEntry entry)
		{
			if (entry == null) throw new ArgumentNullException(nameof(entry));

			Title = Read(entry, PwDefs.TitleField);
			UserName = Read(entry, PwDefs.UserNameField);

			ProtectedString ps = entry.Strings.Get(PwDefs.PasswordField);
			_password = ps != null ? ps.ReadString() : string.Empty;
			_maskedPassword = new string('●', _password.Length);
			IsPasswordVisible = false;
			OnPropertyChanged(nameof(Password));

			Url = Read(entry, PwDefs.UrlField);
			Notes = Read(entry, PwDefs.NotesField);
			LastModified = entry.LastModificationTime;

			if (entry.Expires)
			{
				ExpiryTime = entry.ExpiryTime;
				IsExpired = entry.ExpiryTime < DateTime.UtcNow;
			}
			else
			{
				ExpiryTime = null;
				IsExpired = false;
			}

			CustomFields.Clear();
			foreach (var pair in entry.Strings)
			{
				if (PwDefs.IsStandardField(pair.Key)) continue;
				CustomFields.Add(new FieldViewModel
				{
					Name = pair.Key,
					Value = pair.Value,
					IsProtected = pair.Value.IsProtected
				});
			}

			IsEmpty = false;
		}

		/// <summary>Clears the preview to an empty state.</summary>
		public void Clear()
		{
			Title = string.Empty;
			UserName = string.Empty;
			_password = string.Empty;
			_maskedPassword = string.Empty;
			IsPasswordVisible = false;
			OnPropertyChanged(nameof(Password));
			Url = string.Empty;
			Notes = string.Empty;
			LastModified = DateTime.MinValue;
			ExpiryTime = null;
			IsExpired = false;
			CustomFields.Clear();
			IsEmpty = true;
		}

		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private static string Read(PwEntry entry, string field)
		{
			ProtectedString s = entry.Strings.Get(field);
			return s != null ? s.ReadString() : string.Empty;
		}
	}
}
