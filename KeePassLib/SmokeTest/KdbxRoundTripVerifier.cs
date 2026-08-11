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
using System.IO;
using System.Security.Cryptography;

using KeePassLib.Cryptography;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.SmokeTest
{
	/// <summary>
	/// Verifies KDBX vault round-trip fidelity: opens a golden-file vault,
	/// re-saves it, and asserts that the structure survived intact.
	///
	/// The verifier does NOT perform a byte-for-byte comparison of the encrypted
	/// output because KDBX uses per-save random fields (MasterSeed, EncryptionIV,
	/// and the KDF salt/seed) that change with every write.  Instead it verifies:
	/// <list type="bullet">
	///   <item>The vault can be opened without exception.</item>
	///   <item>The root group, groups, and entry count are preserved.</item>
	///   <item>The re-saved file can be opened again with the same master key.</item>
	///   <item>The entry count after reload equals the count after the first open.</item>
	/// </list>
	/// </summary>
	public static class KdbxRoundTripVerifier
	{
		/// <summary>
		/// Opens the vault at <paramref name="fixturePath"/>, re-saves it to a
		/// temp file, reloads from the temp file, and asserts structural
		/// integrity.  The temp file is deleted on success or failure.
		/// </summary>
		/// <param name="fixturePath">Absolute path to the golden-file KDBX fixture.</param>
		/// <param name="masterPassword">Master password for the fixture.</param>
		/// <exception cref="InvalidOperationException">
		/// Thrown with a descriptive message when any verification step fails.
		/// The message includes the first divergent detail (e.g. entry count mismatch).
		/// </exception>
		public static void Verify(string fixturePath, string masterPassword)
		{
			if(string.IsNullOrEmpty(fixturePath))
				throw new ArgumentNullException(nameof(fixturePath));
			if(!File.Exists(fixturePath))
				throw new FileNotFoundException(
					$"KDBX round-trip verifier: fixture not found at '{fixturePath}'.",
					fixturePath);

			// ── Step 1: open the fixture ─────────────────────────────────────────
			PwDatabase db1 = OpenVault(fixturePath, masterPassword);
			uint groupCount1  = CountGroups(db1.RootGroup);
			uint entryCount1  = CountEntries(db1.RootGroup);

			// ── Step 2: re-save to a temp file ───────────────────────────────────
			string tempPath = Path.Combine(Path.GetTempPath(),
				"kdbx-smoketest-" + Path.GetRandomFileName() + ".kdbx");
			try
			{
				using(FileStream fs = new FileStream(tempPath, FileMode.Create,
					FileAccess.Write, FileShare.None))
				{
					KdbxFile writer = new KdbxFile(db1);
					writer.Save(fs, null, KdbxFormat.Default, null);
				}

				// ── Step 3: reload the saved file ────────────────────────────────
				PwDatabase db2  = OpenVault(tempPath, masterPassword);
				uint groupCount2 = CountGroups(db2.RootGroup);
				uint entryCount2 = CountEntries(db2.RootGroup);

				// ── Step 4: structural assertions ────────────────────────────────
				if(groupCount2 != groupCount1)
					throw new InvalidOperationException(
						$"KDBX round-trip failed for '{Path.GetFileName(fixturePath)}': " +
						$"group count changed from {groupCount1} to {groupCount2} after re-save.");

				if(entryCount2 != entryCount1)
					throw new InvalidOperationException(
						$"KDBX round-trip failed for '{Path.GetFileName(fixturePath)}': " +
						$"entry count changed from {entryCount1} to {entryCount2} after re-save.");

				if(db2.RootGroup == null)
					throw new InvalidOperationException(
						$"KDBX round-trip failed for '{Path.GetFileName(fixturePath)}': " +
						"root group is null after reload.");
			}
			finally
			{
				try { if(File.Exists(tempPath)) File.Delete(tempPath); }
				catch { /* best-effort cleanup */ }
			}
		}

		// ── Private helpers ───────────────────────────────────────────────────── //

		private static PwDatabase OpenVault(string path, string masterPassword)
		{
			PwDatabase db  = new PwDatabase();
			CompositeKey key = new CompositeKey();
			key.AddUserKey(new KcpPassword(masterPassword));
			db.MasterKey = key;

			using(FileStream fs = new FileStream(path, FileMode.Open,
				FileAccess.Read, FileShare.Read))
			{
				KdbxFile kdbx = new KdbxFile(db);
				kdbx.Load(fs, KdbxFormat.Default, null);
			}

			return db;
		}

		private static uint CountGroups(PwGroup root)
		{
			if(root == null) return 0;
			uint n = root.Groups.UCount;
			for(uint i = 0; i < root.Groups.UCount; ++i)
				n += CountGroups(root.Groups.GetAt(i));
			return n;
		}

		private static uint CountEntries(PwGroup root)
		{
			if(root == null) return 0;
			uint n = root.Entries.UCount;
			for(uint i = 0; i < root.Groups.UCount; ++i)
				n += CountEntries(root.Groups.GetAt(i));
			return n;
		}
	}
}
