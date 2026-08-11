using System;
using System.Collections.Generic;

using KeePassLib;
using KeePassLib.Security;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Read-only projection of a <see cref="PwEntry"/> for use in the entry list
	/// panel. Properties are evaluated eagerly so the list can be sorted and
	/// filtered without repeated domain-object traversals.
	/// </summary>
	public sealed class EntryListItemViewModel
	{
		public PwUuid Uuid { get; }

		/// <summary>Entry title as plain text.</summary>
		public string Title { get; }

		/// <summary>User name as plain text.</summary>
		public string UserName { get; }

		/// <summary>URL as plain text.</summary>
		public string Url { get; }

		/// <summary>First line of notes (truncated to 80 chars).</summary>
		public string NotesPreview { get; }

		public PwIcon IconIndex { get; }

		public DateTime LastModificationTime { get; }

		public DateTime? ExpiryTime { get; }

		/// <summary>True when the entry has an expiry date and that date is in the past.</summary>
		public bool IsExpired { get; }

		public IReadOnlyList<string> Tags { get; }

		/// <summary>The underlying entry reference; used for selection by the host VM.</summary>
		internal PwEntry Entry { get; }

		public EntryListItemViewModel(PwEntry entry)
		{
			if (entry == null) throw new ArgumentNullException(nameof(entry));

			Entry = entry;
			Uuid = entry.Uuid;
			Title = Read(entry, PwDefs.TitleField);
			UserName = Read(entry, PwDefs.UserNameField);
			Url = Read(entry, PwDefs.UrlField);
			NotesPreview = TruncateNotes(Read(entry, PwDefs.NotesField));
			IconIndex = entry.IconId;
			LastModificationTime = entry.LastModificationTime;

			if (entry.Expires)
			{
				ExpiryTime = entry.ExpiryTime;
				IsExpired = entry.ExpiryTime < DateTime.UtcNow;
			}

			Tags = new List<string>(entry.Tags).AsReadOnly();
		}

		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private static string Read(PwEntry entry, string field)
		{
			ProtectedString ps = entry.Strings.Get(field);
			return ps != null ? ps.ReadString() : string.Empty;
		}

		private static string TruncateNotes(string notes)
		{
			if (string.IsNullOrEmpty(notes)) return string.Empty;
			int newLine = notes.IndexOf('\n');
			string firstLine = newLine >= 0 ? notes.Substring(0, newLine) : notes;
			return firstLine.Length > 80 ? firstLine.Substring(0, 80) : firstLine;
		}
	}
}
