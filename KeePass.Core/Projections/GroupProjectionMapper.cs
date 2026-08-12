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
using KeePassLib.Collections;

namespace KeePass.Core.Projections
{
	/// <summary>
	/// Maps a <see cref="PwGroup"/> domain object to an immutable
	/// <see cref="GroupProjection"/> snapshot.
	///
	/// <para>Thread-safe: the mapper is stateless and can be shared across threads.</para>
	/// </summary>
	public sealed class GroupProjectionMapper : IProjectionMapper<PwGroup, GroupProjection>
	{
		/// <inheritdoc/>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="source"/> is <c>null</c>.
		/// </exception>
		public GroupProjection FromDomain(PwGroup source)
		{
			if(source == null) throw new ArgumentNullException("source");

			return new GroupProjection
			{
				Uuid            = source.Uuid,
				ParentGroupUuid = source.ParentGroup?.Uuid ?? PwUuid.Zero,

				Name  = source.Name ?? string.Empty,
				Notes = source.Notes ?? string.Empty,

				IconId       = source.IconId,
				CustomIconUuid = source.CustomIconUuid,

				IsExpanded = source.IsExpanded,

				EnableAutoType         = source.EnableAutoType,
				EnableSearching        = source.EnableSearching,
				DefaultAutoTypeSequence = source.DefaultAutoTypeSequence ?? string.Empty,

				Tags = source.Tags != null
					? new List<string>(source.Tags).AsReadOnly()
					: Array.AsReadOnly(new string[0]),

				CreationTime         = source.CreationTime,
				LastModificationTime = source.LastModificationTime,
				ExpiryTime           = source.ExpiryTime,
				Expires              = source.Expires,

				CustomDataKeys = BuildCustomDataKeys(source.CustomData),

				FullPath       = source.GetFullPath(" / ", true),
				Depth          = ComputeDepth(source),
				ChildGroupCount = (int)source.Groups.UCount,
				ChildEntryCount = (int)source.Entries.UCount,
			};
		}

		// ── Private helpers ───────────────────────────────────────────────────

		private static IReadOnlyList<string> BuildCustomDataKeys(StringDictionaryEx customData)
		{
			if(customData == null) return Array.AsReadOnly(new string[0]);
			var keys = new List<string>();
			foreach(KeyValuePair<string, string> kv in customData)
				keys.Add(kv.Key);
			return keys.AsReadOnly();
		}

		/// <summary>
		/// Computes the depth of <paramref name="group"/> by traversing parent
		/// references until the root (a group with no parent) is reached.
		/// Root depth is 0.
		/// </summary>
		private static int ComputeDepth(PwGroup group)
		{
			int depth = 0;
			PwGroup current = group.ParentGroup;
			while(current != null)
			{
				depth++;
				current = current.ParentGroup;
			}
			return depth;
		}
	}
}
