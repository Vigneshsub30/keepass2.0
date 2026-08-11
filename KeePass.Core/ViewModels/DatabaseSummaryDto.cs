/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Immutable summary descriptor of one open (or locked) database document,
	/// used to populate the database tab strip / switcher in the UI.
	/// </summary>
	public sealed class DatabaseSummaryDto
	{
		/// <summary>Display name of the database (file name without path).</summary>
		public string Name { get; init; }

		/// <summary>Full file-system path to the database file, or empty when unsaved.</summary>
		public string Path { get; init; }

		/// <summary>Whether the in-memory database has unsaved changes.</summary>
		public bool IsModified { get; init; }

		/// <summary>Whether the database is currently locked (entry list hidden).</summary>
		public bool IsLocked { get; init; }

		/// <summary>Whether the database is open (as opposed to an empty placeholder slot).</summary>
		public bool IsOpen { get; init; }

		public DatabaseSummaryDto() { }
	}
}
