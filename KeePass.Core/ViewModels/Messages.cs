/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using KeePassLib;
using KeePass.Core.Projections;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Sent via <c>WeakReferenceMessenger</c> when the active database or
	/// document list changes (open, close, lock, unlock, tab switch).
	/// </summary>
	public sealed class DatabaseChangedMessage
	{
		/// <summary>The new active database, or <c>null</c> when no database is open.</summary>
		public PwDatabase ActiveDatabase { get; }

		public DatabaseChangedMessage(PwDatabase activeDatabase)
		{
			ActiveDatabase = activeDatabase;
		}
	}

	/// <summary>
	/// Sent via <c>WeakReferenceMessenger</c> when the user selects a different
	/// group in the group tree.
	/// </summary>
	public sealed class GroupSelectedMessage
	{
		/// <summary>The newly selected group projection, or <c>null</c> when deselected.</summary>
		public GroupProjection Group { get; }

		public GroupSelectedMessage(GroupProjection group)
		{
			Group = group;
		}
	}

	/// <summary>
	/// Sent via <c>WeakReferenceMessenger</c> when the selected entry set changes.
	/// </summary>
	public sealed class EntrySelectedMessage
	{
		/// <summary>The primary selected entry, or <c>null</c> when nothing is selected.</summary>
		public EntryProjection Entry { get; }

		public EntrySelectedMessage(EntryProjection entry)
		{
			Entry = entry;
		}
	}
}
