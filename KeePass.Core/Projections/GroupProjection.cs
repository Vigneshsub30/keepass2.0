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

namespace KeePass.Core.Projections
{
	/// <summary>
	/// Immutable, read-only snapshot of a <see cref="PwGroup"/> suitable for
	/// use in view-models and cross-UI projection layers.
	///
	/// <para>Hierarchy metadata (<see cref="FullPath"/>, <see cref="Depth"/>,
	/// <see cref="ChildGroupCount"/>, <see cref="ChildEntryCount"/>) is
	/// computed at projection time and does not require navigating the live
	/// group tree later.</para>
	/// </summary>
	public sealed class GroupProjection
	{
		// ── Identity ─────────────────────────────────────────────────────────

		/// <summary>The unique identifier of this group.</summary>
		public PwUuid Uuid { get; init; }

		/// <summary>UUID of the parent group, or <see cref="PwUuid.Zero"/> for the root group.</summary>
		public PwUuid ParentGroupUuid { get; init; }

		// ── Display ──────────────────────────────────────────────────────────

		/// <summary>The display name of this group.</summary>
		public string Name { get; init; }

		/// <summary>The notes/description for this group.</summary>
		public string Notes { get; init; }

		// ── Icon ─────────────────────────────────────────────────────────────

		/// <summary>The built-in icon identifier.</summary>
		public PwIcon IconId { get; init; }

		/// <summary>The custom icon UUID, or <see cref="PwUuid.Zero"/> for none.</summary>
		public PwUuid CustomIconUuid { get; init; }

		// ── UI state ─────────────────────────────────────────────────────────

		/// <summary>Whether this group is expanded in the group tree view.</summary>
		public bool IsExpanded { get; init; }

		// ── Auto-type ────────────────────────────────────────────────────────

		/// <summary>
		/// Inherited auto-type enablement for entries in this group,
		/// or <c>null</c> to inherit from the parent group.
		/// </summary>
		public bool? EnableAutoType { get; init; }

		/// <summary>
		/// Inherited search enablement for entries in this group,
		/// or <c>null</c> to inherit from the parent group.
		/// </summary>
		public bool? EnableSearching { get; init; }

		/// <summary>The default auto-type sequence for entries in this group.</summary>
		public string DefaultAutoTypeSequence { get; init; }

		// ── Tags ─────────────────────────────────────────────────────────────

		/// <summary>The list of tags assigned to this group.</summary>
		public IReadOnlyList<string> Tags { get; init; }

		// ── Timestamps ───────────────────────────────────────────────────────

		/// <summary>When the group was created (UTC).</summary>
		public DateTime CreationTime { get; init; }

		/// <summary>When the group was last modified (UTC).</summary>
		public DateTime LastModificationTime { get; init; }

		/// <summary>The expiry timestamp (UTC); meaningful only when <see cref="Expires"/> is true.</summary>
		public DateTime ExpiryTime { get; init; }

		/// <summary>Whether the group has an active expiry date.</summary>
		public bool Expires { get; init; }

		// ── Custom data ──────────────────────────────────────────────────────

		/// <summary>Custom data keys (values intentionally omitted from the projection).</summary>
		public IReadOnlyList<string> CustomDataKeys { get; init; }

		// ── Hierarchy metadata (computed at projection time) ──────────────────

		/// <summary>
		/// Full display path from the root to this group using "/" as separator.
		/// Example: <c>"Personal/Finance/Banks"</c>.
		/// </summary>
		public string FullPath { get; init; }

		/// <summary>
		/// Depth of this group in the tree (0 = root, 1 = child of root, …).
		/// </summary>
		public int Depth { get; init; }

		/// <summary>Number of direct child groups (not recursive).</summary>
		public int ChildGroupCount { get; init; }

		/// <summary>Number of direct child entries (not recursive).</summary>
		public int ChildEntryCount { get; init; }

		public GroupProjection() { }
	}
}
