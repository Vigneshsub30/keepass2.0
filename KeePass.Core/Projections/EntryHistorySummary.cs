/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;

using KeePassLib;

namespace KeePass.Core.Projections
{
	/// <summary>
	/// Lightweight, immutable summary of a single entry in a
	/// <see cref="KeePassLib.PwEntry"/> history list.
	///
	/// <para>Avoids full <c>PwEntry</c> cloning; carries only the information
	/// required to render a history timeline in the UI.</para>
	/// </summary>
	public sealed class EntryHistorySummary
	{
		/// <summary>The UUID of the historical entry snapshot.</summary>
		public PwUuid Uuid { get; init; }

		/// <summary>The timestamp when this history entry was last modified.</summary>
		public DateTime LastModificationTime { get; init; }

		/// <summary>The title of the entry at this point in history.</summary>
		public string Title { get; init; }

		public EntryHistorySummary() { }
	}
}
