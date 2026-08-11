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
using KeePassLib.Security;

namespace KeePass.Core.Projections
{
	/// <summary>
	/// Immutable, read-only snapshot of a <see cref="PwEntry"/> suitable for
	/// use in view-models and cross-UI projection layers.
	///
	/// <para>All collection properties are <see cref="IReadOnlyList{T}"/> or
	/// <see cref="IReadOnlyDictionary{TKey,TValue}"/> so UI code cannot mutate
	/// them accidentally.  <see cref="System.Drawing.Color"/> values are
	/// represented as nullable 6-digit hex strings (e.g. <c>"FF0000"</c>) or
	/// <c>null</c> when the source color is empty.</para>
	/// </summary>
	public sealed class EntryProjection
	{
		// ── Identity ─────────────────────────────────────────────────────────

		/// <summary>The unique identifier of this entry.</summary>
		public PwUuid Uuid { get; init; }

		/// <summary>UUID of the parent group, or <see cref="PwUuid.Zero"/> when none.</summary>
		public PwUuid ParentGroupUuid { get; init; }

		// ── Standard string fields ────────────────────────────────────────────

		/// <summary>The entry title (may be protected).</summary>
		public ProtectedString Title { get; init; }

		/// <summary>The user name (may be protected).</summary>
		public ProtectedString UserName { get; init; }

		/// <summary>The password (always protected).</summary>
		public ProtectedString Password { get; init; }

		/// <summary>The URL (may be protected).</summary>
		public ProtectedString Url { get; init; }

		/// <summary>The notes field (may be protected).</summary>
		public ProtectedString Notes { get; init; }

		/// <summary>The override URL (plain string, rarely used).</summary>
		public string OverrideUrl { get; init; }

		// ── Custom string fields ──────────────────────────────────────────────

		/// <summary>
		/// All custom (non-standard) string fields keyed by name.
		/// Standard fields (Title, UserName, Password, URL, Notes) are excluded.
		/// </summary>
		public IReadOnlyDictionary<string, ProtectedString> CustomFields { get; init; }

		// ── Icon ─────────────────────────────────────────────────────────────

		/// <summary>The built-in icon identifier.</summary>
		public PwIcon IconId { get; init; }

		/// <summary>The custom icon UUID, or <see cref="PwUuid.Zero"/> for none.</summary>
		public PwUuid CustomIconUuid { get; init; }

		// ── Colors (hex strings or null) ─────────────────────────────────────

		/// <summary>
		/// Foreground colour as a 6-digit uppercase hex string (e.g. <c>"FF0000"</c>),
		/// or <c>null</c> when the entry has no foreground colour.
		/// </summary>
		public string ForegroundColorHex { get; init; }

		/// <summary>
		/// Background colour as a 6-digit uppercase hex string (e.g. <c>"FFFF00"</c>),
		/// or <c>null</c> when the entry has no background colour.
		/// </summary>
		public string BackgroundColorHex { get; init; }

		// ── Tags ─────────────────────────────────────────────────────────────

		/// <summary>The sorted list of tags assigned to this entry.</summary>
		public IReadOnlyList<string> Tags { get; init; }

		// ── Timestamps ───────────────────────────────────────────────────────

		/// <summary>When the entry was created (UTC).</summary>
		public DateTime CreationTime { get; init; }

		/// <summary>When the entry was last modified (UTC).</summary>
		public DateTime LastModificationTime { get; init; }

		/// <summary>When the entry was last accessed (UTC).</summary>
		public DateTime LastAccessTime { get; init; }

		/// <summary>The expiry timestamp (UTC); meaningful only when <see cref="Expires"/> is true.</summary>
		public DateTime ExpiryTime { get; init; }

		/// <summary>Whether the entry has an active expiry date.</summary>
		public bool Expires { get; init; }

		// ── Usage & quality ──────────────────────────────────────────────────

		/// <summary>The number of times the entry has been used (auto-typed or copied).</summary>
		public ulong UsageCount { get; init; }

		/// <summary>Whether password quality checks are enabled for this entry.</summary>
		public bool QualityCheck { get; init; }

		// ── Auto-type ────────────────────────────────────────────────────────

		/// <summary>Whether auto-type is enabled for this entry.</summary>
		public bool AutoTypeEnabled { get; init; }

		/// <summary>The default auto-type keystroke sequence, or empty for the global default.</summary>
		public string AutoTypeSequence { get; init; }

		// ── Custom data ──────────────────────────────────────────────────────

		/// <summary>Custom data keys (values are intentionally omitted from the projection).</summary>
		public IReadOnlyList<string> CustomDataKeys { get; init; }

		// ── History ──────────────────────────────────────────────────────────

		/// <summary>
		/// Lightweight summaries of this entry's history snapshots.
		/// Full snapshot data is not projected to avoid expensive cloning.
		/// </summary>
		public IReadOnlyList<EntryHistorySummary> History { get; init; }

		// ── Binaries ─────────────────────────────────────────────────────────

		/// <summary>
		/// Reference descriptors for binary attachments.
		/// Byte content is not projected; only name, size, and hash are captured.
		/// </summary>
		public IReadOnlyList<BinaryReference> Binaries { get; init; }

		public EntryProjection() { }
	}
}
