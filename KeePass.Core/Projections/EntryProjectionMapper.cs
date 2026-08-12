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
using System.Globalization;
using System.Security.Cryptography;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePass.Core.Projections
{
	/// <summary>
	/// Maps a <see cref="PwEntry"/> domain object to an immutable
	/// <see cref="EntryProjection"/> snapshot.
	///
	/// <para>Thread-safe: the mapper is stateless and can be shared across threads.</para>
	/// </summary>
	public sealed class EntryProjectionMapper : IProjectionMapper<PwEntry, EntryProjection>
	{
		/// <summary>Standard field keys defined by the KeePass entry format.</summary>
		private static readonly HashSet<string> StandardFieldKeys = new HashSet<string>
		{
			PwDefs.TitleField,
			PwDefs.UserNameField,
			PwDefs.PasswordField,
			PwDefs.UrlField,
			PwDefs.NotesField,
		};

		/// <inheritdoc/>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="source"/> is <c>null</c>.
		/// </exception>
		public EntryProjection FromDomain(PwEntry source)
		{
			if(source == null) throw new ArgumentNullException("source");

			return new EntryProjection
			{
				Uuid              = source.Uuid,
				ParentGroupUuid   = source.ParentGroup?.Uuid ?? PwUuid.Zero,

				Title             = source.Strings.GetSafe(PwDefs.TitleField),
				UserName          = source.Strings.GetSafe(PwDefs.UserNameField),
				Password          = source.Strings.GetSafe(PwDefs.PasswordField),
				Url               = source.Strings.GetSafe(PwDefs.UrlField),
				Notes             = source.Strings.GetSafe(PwDefs.NotesField),
				OverrideUrl       = source.OverrideUrl,

				CustomFields      = BuildCustomFields(source.Strings),

				IconId            = source.IconId,
				CustomIconUuid    = source.CustomIconUuid,

				// Color properties are only available when System.Drawing is present
				// (i.e. net10.0-windows / non-UAP builds). In cross-platform builds
				// (net10.0 with KeePassUAP defined) these properties do not exist;
				// the projection always stores null in that case.
				ForegroundColorHex = null,
				BackgroundColorHex = null,

				Tags              = source.Tags != null
					? new List<string>(source.Tags).AsReadOnly()
					: Array.AsReadOnly(new string[0]),

				CreationTime          = source.CreationTime,
				LastModificationTime  = source.LastModificationTime,
				LastAccessTime        = source.LastAccessTime,
				ExpiryTime            = source.ExpiryTime,
				Expires               = source.Expires,

				UsageCount    = source.UsageCount,
				QualityCheck  = source.QualityCheck,

				AutoTypeEnabled   = source.AutoType.Enabled,
				AutoTypeSequence  = source.AutoType.DefaultSequence ?? string.Empty,

				CustomDataKeys = BuildCustomDataKeys(source.CustomData),

				History  = BuildHistory(source.History),
				Binaries = BuildBinaries(source.Binaries),
			};
		}

		// ── Private helpers ───────────────────────────────────────────────────

		private static IReadOnlyDictionary<string, ProtectedString> BuildCustomFields(
			ProtectedStringDictionary strings)
		{
			var dict = new Dictionary<string, ProtectedString>();
			foreach(KeyValuePair<string, ProtectedString> kv in strings)
			{
				if(!StandardFieldKeys.Contains(kv.Key))
					dict[kv.Key] = kv.Value;
			}
			return dict;
		}

		private static IReadOnlyList<string> BuildCustomDataKeys(StringDictionaryEx customData)
		{
			if(customData == null) return Array.AsReadOnly(new string[0]);
			var keys = new List<string>();
			foreach(KeyValuePair<string, string> kv in customData)
				keys.Add(kv.Key);
			return keys.AsReadOnly();
		}

		private static IReadOnlyList<EntryHistorySummary> BuildHistory(
			PwObjectList<PwEntry> history)
		{
			if(history == null || history.UCount == 0)
				return Array.AsReadOnly(new EntryHistorySummary[0]);

			var list = new List<EntryHistorySummary>((int)history.UCount);
			for(uint i = 0; i < history.UCount; i++)
			{
				PwEntry h = history.GetAt(i);
				list.Add(new EntryHistorySummary
				{
					Uuid                 = h.Uuid,
					LastModificationTime = h.LastModificationTime,
					Title                = h.Strings.ReadSafe(PwDefs.TitleField),
				});
			}
			return list.AsReadOnly();
		}

		private static IReadOnlyList<BinaryReference> BuildBinaries(
			ProtectedBinaryDictionary binaries)
		{
			if(binaries == null || binaries.UCount == 0)
				return Array.AsReadOnly(new BinaryReference[0]);

			var list = new List<BinaryReference>((int)binaries.UCount);
			foreach(KeyValuePair<string, ProtectedBinary> kv in binaries)
			{
				byte[] data = kv.Value.ReadData();
				string hash = ComputeSha256Hex(data);
				list.Add(new BinaryReference
				{
					Name        = kv.Key,
					Size        = data != null ? data.Length : 0L,
					ContentHash = hash,
				});
				if(data != null) MemUtil.ZeroByteArray(data);
			}
			return list.AsReadOnly();
		}

		private static string ComputeSha256Hex(byte[] data)
		{
			if(data == null || data.Length == 0) return string.Empty;
			using(SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(data);
				return BytesToHex(hash);
			}
		}

		private static string BytesToHex(byte[] data)
		{
			var sb = new System.Text.StringBuilder(data.Length * 2);
			foreach(byte b in data)
				sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
			return sb.ToString();
		}
	}
}
