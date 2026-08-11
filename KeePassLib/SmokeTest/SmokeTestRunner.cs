/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

namespace KeePassLib.SmokeTest
{
	/// <summary>
	/// Orchestrates the KeePass application smoke test:
	/// <list type="number">
	///   <item>Run <see cref="SelfTest.Perform()"/> and assert expected test count.</item>
	///   <item>Generate a minimal KDBX fixture for each format variant.</item>
	///   <item>Run <see cref="KdbxRoundTripVerifier.Verify"/> on each fixture.</item>
	/// </list>
	///
	/// Exit codes (returned by <see cref="Run"/>):
	/// <list type="bullet">
	///   <item><c>0</c> — all tests passed.</item>
	///   <item><c>1</c> — <see cref="SelfTest.Perform()"/> failed (crypto vector error).</item>
	///   <item><c>2</c> — KDBX round-trip test failed (open / re-save / reload mismatch).</item>
	///   <item><c>3</c> — fixture generation or file system error.</item>
	/// </list>
	/// </summary>
	public static class SmokeTestRunner
	{
		/// <summary>Exit code returned when all smoke tests pass.</summary>
		public const int ExitSuccess = 0;
		/// <summary>Exit code returned when the cryptographic self-test fails.</summary>
		public const int ExitSelfTestFailed = 1;
		/// <summary>Exit code returned when any KDBX round-trip test fails.</summary>
		public const int ExitRoundTripFailed = 2;
		/// <summary>Exit code returned when fixture setup fails.</summary>
		public const int ExitFixtureFailed = 3;

		private const string MasterPassword = "SmokeTestPassword42!";

		/// <summary>
		/// Run the full smoke test suite, printing results to
		/// <see cref="Console.Out"/> and <see cref="Console.Error"/>.
		/// </summary>
		/// <returns>An exit code as defined by the <c>Exit*</c> constants.</returns>
		public static int Run()
		{
			Console.WriteLine("=== KeePass Smoke Test ===");

			// ── Phase 1: cryptographic self-test ────────────────────────────── //
			int selfTestCount;
			try
			{
				Console.Write("Phase 1/3 — Running SelfTest.Perform() ... ");
				selfTestCount = SelfTest.Perform();
				Console.WriteLine($"OK ({selfTestCount} test methods)");

				if(selfTestCount < SelfTest.ExpectedTestCount)
				{
					string msg = $"SelfTest ran {selfTestCount} test methods; " +
						$"expected at least {SelfTest.ExpectedTestCount}. " +
						"Some tests may have been inadvertently removed.";
					Console.Error.WriteLine($"FAIL: {msg}");
					return ExitSelfTestFailed;
				}
			}
			catch(Exception ex)
			{
				Console.Error.WriteLine($"FAIL — SelfTest.Perform() threw: {ex.Message}");
				return ExitSelfTestFailed;
			}

			// ── Phase 2: generate temporary KDBX fixtures ───────────────────── //
			string tempDir = Path.Combine(Path.GetTempPath(),
				"KeePass_SmokeTest_" + Path.GetRandomFileName());

			Console.Write("Phase 2/3 — Generating KDBX fixtures ... ");
			List<(string Path, string Password)> fixtures;
			try
			{
				fixtures = GenerateFixtures(tempDir);
				Console.WriteLine($"OK ({fixtures.Count} fixtures in {tempDir})");
			}
			catch(Exception ex)
			{
				Console.Error.WriteLine($"FAIL — fixture generation: {ex.Message}");
				return ExitFixtureFailed;
			}

			// ── Phase 3: round-trip each fixture ────────────────────────────── //
			Console.WriteLine("Phase 3/3 — KDBX round-trip verification:");
			int roundTripFails = 0;
			try
			{
				foreach((string path, string password) in fixtures)
				{
					string name = Path.GetFileName(path);
					Console.Write($"  {name} ... ");
					try
					{
						KdbxRoundTripVerifier.Verify(path, password);
						Console.WriteLine("OK");
					}
					catch(Exception ex)
					{
						Console.WriteLine($"FAIL");
						Console.Error.WriteLine($"  ERROR: {ex.Message}");
						++roundTripFails;
					}
				}
			}
			finally
			{
				try { if(Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); }
				catch { /* best-effort cleanup */ }
			}

			if(roundTripFails > 0)
			{
				Console.Error.WriteLine($"FAIL — {roundTripFails} round-trip test(s) failed.");
				return ExitRoundTripFailed;
			}

			Console.WriteLine("=== All smoke tests PASSED ===");
			return ExitSuccess;
		}

		// ── Fixture generation ────────────────────────────────────────────────── //

		private static List<(string, string)> GenerateFixtures(string dir)
		{
			Directory.CreateDirectory(dir);
			var result = new List<(string, string)>();

			// KDBX 3.1 — AES-CBC + AES-KDF + GZip (uses Salsa20 inner stream)
			result.Add(GenerateFixture(dir, "kdbx31-aes-aeskdf-gzip.kdbx",
				cipherUuid: StandardAesEngine.AesUuid,
				kdfName: "AesKdf",
				compression: PwCompressionAlgorithm.GZip));

			// KDBX 4.0 — AES-CBC + Argon2id + GZip
			result.Add(GenerateFixture(dir, "kdbx40-aes-argon2id-gzip.kdbx",
				cipherUuid: StandardAesEngine.AesUuid,
				kdfName: "Argon2id",
				compression: PwCompressionAlgorithm.GZip));

			// KDBX 4.1 — ChaCha20 + Argon2id + GZip (requires group tags / named icon)
			result.Add(GenerateFixture(dir, "kdbx41-chacha20-argon2id-gzip.kdbx",
				cipherUuid: ChaCha20Uuid,
				kdfName: "Argon2id",
				compression: PwCompressionAlgorithm.GZip,
				include41Features: true));

			return result;
		}

		private static (string, string) GenerateFixture(
			string dir,
			string name,
			PwUuid cipherUuid,
			string kdfName,
			PwCompressionAlgorithm compression,
			bool include41Features = false)
		{
			PwDatabase db = new PwDatabase();
			db.DataCipherUuid = cipherUuid;
			db.Compression    = compression;
			db.KdfParameters  = BuildKdf(kdfName);
			db.Name           = $"SmokeTest-{name}";

			PwGroup root   = new PwGroup(true, true, "Root",   PwIcon.Folder);
			PwGroup group1 = new PwGroup(true, true, "Grp1",   PwIcon.Folder);
			PwGroup group2 = new PwGroup(true, true, "Grp2",   PwIcon.Folder);
			db.RootGroup = root;
			root.AddGroup(group1, true);
			root.AddGroup(group2, true);

			if(include41Features)
			{
				group1.Tags.Add("smoke");
				group2.Tags.Add("test");
			}

			AddEntry(root,   "Entry-Root",  "root-user",  "root-pass!");
			AddEntry(group1, "Entry-Grp1A", "alice",      "passA!");
			AddEntry(group1, "Entry-Grp1B", "bob",        "passB!");
			AddEntry(group2, "Entry-Grp2",  "carol",      "passC!");

			// Entry with attachment (exercises binary serialisation)
			PwEntry attachEntry = MakeEntry("Attached", "user", "att-pass!");
			attachEntry.Binaries.Set("note.txt",
				new ProtectedBinary(false,
					Encoding.UTF8.GetBytes("Smoke test attachment.")));
			root.AddEntry(attachEntry, true);

			// Custom icon (exercises custom icon serialisation)
			PwUuid iconId = new PwUuid(true);
			PwCustomIcon icon = new PwCustomIcon(iconId, MinimalPng);
			if(include41Features)
			{
				icon.Name = "SmokeIcon";
				icon.LastModificationTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			}
			db.CustomIcons.Add(icon);

			string path = Path.Combine(dir, name);
			using(FileStream fs = new FileStream(path, FileMode.Create,
				FileAccess.Write, FileShare.None))
			{
				new KdbxFile(db).Save(fs, null, KdbxFormat.Default, null);
			}

			return (path, MasterPassword);
		}

		private static KdfParameters BuildKdf(string kdfName)
		{
			switch(kdfName)
			{
				case "AesKdf":
				{
					AesKdf kdf = new AesKdf();
					KdfParameters p = kdf.GetDefaultParameters();
					// Low round count keeps smoke test fast (<60 s total).
					p.SetUInt64(AesKdf.ParamRounds, 6000);
					return p;
				}
				case "Argon2id":
				{
					Argon2Kdf kdf = new Argon2Kdf(Argon2Type.ID);
					KdfParameters p = kdf.GetDefaultParameters();
					kdf.Randomize(p);
					// 8 MB / 1 iteration — fast, fits CI runners with 2 GB RAM.
					p.SetUInt64(Argon2Kdf.ParamMemory, 8 * 1024);
					p.SetUInt64(Argon2Kdf.ParamIterations, 1);
					p.SetUInt32(Argon2Kdf.ParamParallelism, 1);
					return p;
				}
				default:
					throw new ArgumentException($"Unknown KDF for smoke test: {kdfName}");
			}
		}

		private static void AddEntry(PwGroup group, string title, string user, string pass)
		{
			group.AddEntry(MakeEntry(title, user, pass), true);
		}

		private static PwEntry MakeEntry(string title, string user, string pass)
		{
			PwEntry e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, title));
			e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, user));
			e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, pass));
			return e;
		}

		// ChaCha20 cipher UUID (from the KeePass KDBX format specification)
		private static readonly PwUuid ChaCha20Uuid = new PwUuid(new byte[]
		{
			0xD6, 0x03, 0x8A, 0x2B, 0x8B, 0x6F, 0x4C, 0xB5,
			0xA5, 0x24, 0x33, 0x9A, 0x31, 0xDB, 0xB5, 0x9A
		});

		// Minimal 1×1 blue PNG (67 bytes, no System.Drawing dependency)
		private static readonly byte[] MinimalPng = new byte[]
		{
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
			0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
			0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
			0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
			0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
			0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
			0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
			0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
			0x44, 0xAE, 0x42, 0x60, 0x82
		};
	}
}
