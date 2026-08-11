using System;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Generates in-memory <see cref="PwDatabase"/> instances with configurable
	/// groups, entries, and attachments for use in UI tests.
	/// </summary>
	public static class TestDatabaseFactory
	{
		/// <summary>
		/// Creates a database with a two-level group hierarchy and a set of
		/// sample entries suitable for testing the main-window panels.
		/// </summary>
		/// <param name="name">Database file name (without path).</param>
		/// <param name="groupDepth">Depth of the group hierarchy (1 = root only).</param>
		/// <param name="entriesPerGroup">Entries to add to every non-root group.</param>
		public static PwDatabase CreateSample(
			string name = "TestDb.kdbx",
			int groupDepth = 2,
			int entriesPerGroup = 3)
		{
			var db = new PwDatabase();
			db.New(new IOConnectionInfo { Path = name }, new CompositeKey());

			var root = db.RootGroup;
			root.Name = "KeePass Test Database";

			for (int g = 1; g <= groupDepth; g++)
			{
				var group = new PwGroup(true, true, $"Group {g}", PwIcon.Folder);
				root.AddGroup(group, true);

				for (int e = 1; e <= entriesPerGroup; e++)
				{
					group.AddEntry(MakeEntry($"Entry {g}.{e}", $"user{e}", $"pass{g}{e}",
						$"https://example.com/{g}/{e}", $"Note for entry {e} in group {g}"), true);
				}
			}

			return db;
		}

		/// <summary>
		/// Creates a minimal single-group database with no entries.
		/// </summary>
		public static PwDatabase CreateEmpty(string name = "Empty.kdbx")
		{
			var db = new PwDatabase();
			db.New(new IOConnectionInfo { Path = name }, new CompositeKey());
			db.RootGroup.Name = "Empty Database";
			return db;
		}

		/// <summary>
		/// Creates a database with an expired entry for testing expiry indicators.
		/// </summary>
		public static PwDatabase CreateWithExpiredEntry(string name = "Expired.kdbx")
		{
			var db = new PwDatabase();
			db.New(new IOConnectionInfo { Path = name }, new CompositeKey());

			var entry = MakeEntry("Expired Login", "user", "pass", "https://example.com", "Old entry");
			entry.Expires = true;
			entry.ExpiryTime = DateTime.UtcNow.AddDays(-1);

			db.RootGroup.AddEntry(entry, true);
			return db;
		}

		// ------------------------------------------------------------------ //
		// Private helpers                                                      //
		// ------------------------------------------------------------------ //

		private static PwEntry MakeEntry(
			string title, string user, string pass, string url, string notes)
		{
			var e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, title));
			e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, user));
			e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  pass));
			e.Strings.Set(PwDefs.UrlField,      new ProtectedString(false, url));
			e.Strings.Set(PwDefs.NotesField,    new ProtectedString(false, notes));
			return e;
		}
	}
}
