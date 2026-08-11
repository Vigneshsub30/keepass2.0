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

using KeePassLib;

using KeePass.Core.ViewModels;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Abstracts the <c>DocumentManagerEx</c> document-management layer so that
	/// view-model code in <c>KeePass.Core</c> can drive database lifecycle
	/// operations without a hard dependency on <c>System.Windows.Forms</c>.
	///
	/// <para>
	/// Implementations are responsible for translating ViewModel intent (open,
	/// close, save, lock) into the appropriate UI interactions (showing dialogs,
	/// updating tab strips, etc.).
	/// </para>
	/// </summary>
	public interface IDatabaseSessionService
	{
		// ── State queries ─────────────────────────────────────────────────────

		/// <summary>
		/// Returns summary descriptors for every currently-open document slot
		/// (including locked or empty placeholder slots).
		/// </summary>
		IReadOnlyList<DatabaseSummaryDto> GetDocuments();

		/// <summary>
		/// Zero-based index of the currently active document within the list
		/// returned by <see cref="GetDocuments()"/>.
		/// </summary>
		int ActiveDocumentIndex { get; }

		/// <summary>
		/// Returns the live <see cref="PwDatabase"/> for the currently active
		/// document, or <c>null</c> when no database is open.
		/// </summary>
		PwDatabase GetActiveDatabase();

		/// <summary>
		/// Returns the live <see cref="PwDatabase"/> for the document at the
		/// given index, or <c>null</c> when that slot has no open database.
		/// </summary>
		PwDatabase GetDatabase(int index);

		/// <summary>Whether the currently active database is locked.</summary>
		bool IsActiveDatabaseLocked { get; }

		// ── Change notification ───────────────────────────────────────────────

		/// <summary>
		/// Raised when the active document changes, a database is opened or
		/// closed, or the lock/unlock state transitions.
		/// </summary>
		event EventHandler SessionChanged;

		// ── Operations (intent signals — UI layer handles dialogs) ────────────

		/// <summary>Switches the active document to the one at <paramref name="index"/>.</summary>
		void SetActiveDocument(int index);

		/// <summary>Initiates a new-database workflow.</summary>
		void CreateNew();

		/// <summary>Initiates an open-database workflow.</summary>
		void OpenDatabase();

		/// <summary>Closes the currently active database.</summary>
		void CloseDatabase();

		/// <summary>Saves the currently active database.</summary>
		void SaveDatabase();

		/// <summary>Locks the currently active workspace.</summary>
		void LockWorkspace();

		/// <summary>Unlocks the currently active workspace.</summary>
		void UnlockWorkspace();
	}
}
