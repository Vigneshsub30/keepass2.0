/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using CommunityToolkit.Mvvm.ComponentModel;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model representing a single auto-type window/sequence association.
	/// </summary>
	public sealed class AutoTypeAssociationViewModel : ObservableObject
	{
		private string _windowName;
		private string _sequence;

		/// <summary>
		/// The window title pattern to match (may contain wildcards).
		/// Empty means "match all windows" (global default sequence override).
		/// </summary>
		public string WindowName
		{
			get { return _windowName; }
			set { SetProperty(ref _windowName, value ?? string.Empty); }
		}

		/// <summary>
		/// The keystroke sequence to use for this window (empty = use entry default).
		/// </summary>
		public string Sequence
		{
			get { return _sequence; }
			set { SetProperty(ref _sequence, value ?? string.Empty); }
		}

		public AutoTypeAssociationViewModel() { }

		public AutoTypeAssociationViewModel(string windowName, string sequence)
		{
			_windowName = windowName ?? string.Empty;
			_sequence   = sequence   ?? string.Empty;
		}
	}
}
