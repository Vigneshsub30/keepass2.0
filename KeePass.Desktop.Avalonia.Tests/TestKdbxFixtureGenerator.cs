using System;
using System.IO;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Creates a test KDBX file with a known password at a deterministic path
	/// under the test output directory.  Callers use <see cref="EnsureFixture"/>
	/// to get the path, generating the file on first call and reusing it
	/// afterwards.
	///
	/// <para>
	/// Known credentials: password = <c>TestPassword123</c>
	/// </para>
	/// </summary>
	public static class TestKdbxFixtureGenerator
	{
		public const string KnownPassword = "TestPassword123";

		private static readonly string FixturePath = Path.Combine(
			AppContext.BaseDirectory, "Fixtures", "test-fixture.kdbx");

		/// <summary>
		/// Ensures the KDBX fixture file exists and returns its absolute path.
		/// Thread-safe — the file is only written once per test run.
		/// </summary>
		public static string EnsureFixture()
		{
			if (File.Exists(FixturePath)) return FixturePath;

			Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
			CreateFixture(FixturePath, KnownPassword);
			return FixturePath;
		}

		// ------------------------------------------------------------------ //
		// Private helpers                                                      //
		// ------------------------------------------------------------------ //

		private static void CreateFixture(string path, string password)
		{
			var db = new PwDatabase();
			var key = new CompositeKey();
			key.AddUserKey(new KcpPassword(System.Text.Encoding.UTF8.GetBytes(password)));

			db.New(new IOConnectionInfo { Path = path }, key);

			db.RootGroup.Name = "Test Fixture";

			var entry = new PwEntry(true, true);
			entry.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Sample Login"));
			entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "testuser"));
			entry.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "secret"));
			entry.Strings.Set(PwDefs.UrlField,      new ProtectedString(false, "https://example.com"));
			db.RootGroup.AddEntry(entry, true);

			db.Save(null);
		}
	}
}
