/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using CommunityToolkit.Mvvm.ComponentModel;

using KeePassLib.Security;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model representing a single custom string field in an entry editor.
	/// Supports observable Name, Value, and protection-flag changes.
	/// </summary>
	public sealed class FieldViewModel : ObservableObject
	{
		private string _name;
		private ProtectedString _value;
		private bool _isProtected;

		/// <summary>The field name (key), e.g. <c>"TOTP"</c>.</summary>
		public string Name
		{
			get { return _name; }
			set { SetProperty(ref _name, value); }
		}

		/// <summary>The field value (may be protected/secret).</summary>
		public ProtectedString Value
		{
			get { return _value; }
			set { SetProperty(ref _value, value); }
		}

		/// <summary>
		/// Plain-text value for UI display. Shows "••••••" for protected fields.
		/// The setter updates <see cref="Value"/> immediately.
		/// </summary>
		public string ValueText
		{
			get
			{
				if (_value == null) return string.Empty;
				return _isProtected ? "••••••" : _value.ReadString();
			}
			set
			{
				Value = new ProtectedString(_isProtected, value ?? string.Empty);
				OnPropertyChanged(nameof(ValueText));
			}
		}

		/// <summary>Whether the field value should be hidden in the UI.</summary>
		public bool IsProtected
		{
			get { return _isProtected; }
			set
			{
				if(SetProperty(ref _isProtected, value) && _value != null)
				{
					// Re-wrap the ProtectedString with the new protection state
					Value = new ProtectedString(value, _value.ReadString());
				}
			}
		}

		public FieldViewModel() { }

		public FieldViewModel(string name, ProtectedString value)
		{
			_name       = name ?? string.Empty;
			_value      = value ?? ProtectedString.Empty;
			_isProtected = _value.IsProtected;
		}
	}
}
