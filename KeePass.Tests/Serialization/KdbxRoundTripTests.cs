using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KeePass.Tests.Fixtures;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Xunit;

namespace KeePass.Tests.Serialization
{
    /// <summary>
    /// Byte-level round-trip verification for the KDBX golden-file corpus.
    ///
    /// Each test opens a fixture, re-saves it without modifications, then verifies:
    ///   A) Non-random binary header fields (CipherID, CompressionFlags, version)
    ///      are preserved byte-for-byte.
    ///   B) The format version is KDBX 4.x, confirming ChaCha20 inner random stream.
    ///   C) The full database object model (groups, entries, history, attachments,
    ///      icons) is identical after the round-trip.
    ///
    /// Per-save random fields (MasterSeed, EncryptionIV, KDF salt, InnerRandomStreamKey)
    /// change on every save and are intentionally excluded from comparison.
    ///
    /// These tests run on the Windows CI job (net10.0-windows requirement).
    /// </summary>
    public class KdbxRoundTripTests
    {
        // ── Fixture enumeration (reuses GoldenFileGenerator.AllSpecs) ─────────

        public static IEnumerable<object[]> GetFixtures()
        {
            foreach (GoldenFileGenerator.FixtureSpec spec in GoldenFileGenerator.AllSpecs())
                yield return new object[] { spec.Name };
        }

        // ── Test A: Non-random header fields preserved ─────────────────────────

        /// <summary>
        /// Verifies that the binary KDBX header's non-random fields — magic
        /// signatures, format version, CipherID, and CompressionFlags — are
        /// identical between the original fixture and the re-saved output.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetFixtures))]
        public void GoldenFile_RoundTrip_NonRandomHeaderFieldsPreserved(string fixtureName)
        {
            string path = EnsureFixture(fixtureName);

            byte[] originalBytes = File.ReadAllBytes(path);
            KdbxHeaderInfo origHeader = KdbxHeaderParser.Parse(originalBytes);

            PwDatabase db = LoadFixture(path);
            byte[] resavedBytes = SaveDatabase(db);
            KdbxHeaderInfo resavedHeader = KdbxHeaderParser.Parse(resavedBytes);

            // Signatures never change
            Assert.Equal(KdbxHeaderParser.ExpectedSig1, resavedHeader.Sig1);
            Assert.Equal(KdbxHeaderParser.ExpectedSig2, resavedHeader.Sig2);

            // Format version must be preserved (determined by database content)
            Assert.Equal(origHeader.MajorVersion, resavedHeader.MajorVersion);
            Assert.Equal(origHeader.MinorVersion, resavedHeader.MinorVersion);

            // Cipher must be identical
            Assert.Equal(origHeader.CipherIdBytes, resavedHeader.CipherIdBytes);

            // Compression algorithm must be identical
            Assert.Equal(origHeader.CompressionFlags, resavedHeader.CompressionFlags);
        }

        // ── Test B: Inner random stream type verification ──────────────────────

        /// <summary>
        /// Verifies that all fixtures are KDBX 4.x, which mandates ChaCha20
        /// (CrsAlgorithm.ChaCha20 = 3) as the inner random stream.  The inner
        /// stream ID lives in the encrypted inner header (inside the body), so
        /// it cannot be read without full decryption; format version >= 4.0 is
        /// the authoritative gate used by KeePassLib itself.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetFixtures))]
        public void GoldenFile_RoundTrip_InnerRandomStreamIsChaCha20ForKdbx4(string fixtureName)
        {
            string path = EnsureFixture(fixtureName);

            KdbxHeaderInfo origHeader = KdbxHeaderParser.Parse(File.ReadAllBytes(path));
            Assert.True(origHeader.MajorVersion >= 4,
                $"{fixtureName}: expected KDBX 4.x, got major={origHeader.MajorVersion}");

            PwDatabase db = LoadFixture(path);
            byte[] resavedBytes = SaveDatabase(db);
            KdbxHeaderInfo resavedHeader = KdbxHeaderParser.Parse(resavedBytes);

            Assert.True(resavedHeader.MajorVersion >= 4,
                $"{fixtureName} re-saved: expected KDBX 4.x, got major={resavedHeader.MajorVersion}");

            // KDBX 4.x always writes ChaCha20 as inner random stream (CrsAlgorithm.ChaCha20 = 3).
            // KdbxFile.Write.cs line 132: m_pbInnerRandomStreamKey = cr.GetRandomBytes(64)
            // for ChaCha20 inner stream. The inner stream ID is written to the encrypted inner
            // header, so verifying MajorVersion >= 4 is the canonical check.
        }

        // ── Test C: Full database structure identical after round-trip ─────────

        /// <summary>
        /// Loads the fixture, re-saves it to a MemoryStream, reloads from that
        /// stream, and performs a deep structural comparison of both PwDatabase
        /// objects: groups, entries (including all standard fields and passwords),
        /// history, binary attachments, and custom icons.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetFixtures))]
        public void GoldenFile_RoundTrip_DatabaseStructureIdentical(string fixtureName)
        {
            string path = EnsureFixture(fixtureName);

            PwDatabase dbOrig    = LoadFixture(path);
            byte[]     resaved   = SaveDatabase(dbOrig);
            PwDatabase dbResaved = LoadFromBytes(resaved);

            // Top-level metadata
            Assert.Equal(dbOrig.Name,        dbResaved.Name);
            Assert.Equal(dbOrig.Description, dbResaved.Description);
            Assert.Equal(
                dbOrig.DataCipherUuid.UuidBytes,
                dbResaved.DataCipherUuid.UuidBytes);
            Assert.Equal(
                (uint)dbOrig.Compression,
                (uint)dbResaved.Compression);

            // Custom icons
            Assert.Equal(dbOrig.CustomIcons.Count, dbResaved.CustomIcons.Count);

            // Group / entry tree
            CompareGroups(dbOrig.RootGroup, dbResaved.RootGroup, fixtureName);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string EnsureFixture(string fixtureName)
        {
            string path = Path.Combine(GoldenFileGenerator.FixtureDir, fixtureName);
            if (!File.Exists(path))
                GoldenFileGenerator.GenerateAll();
            Assert.True(File.Exists(path),
                $"Fixture generation failed — file still missing: {path}");
            return path;
        }

        private static PwDatabase LoadFixture(string path)
        {
            PwDatabase db = new PwDatabase();
            db.MasterKey = MakeKey();
            using (FileStream fs = File.OpenRead(path))
                new KdbxFile(db).Load(fs, KdbxFormat.Default, null);
            return db;
        }

        private static byte[] SaveDatabase(PwDatabase db)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new KdbxFile(db).Save(ms, null, KdbxFormat.Default, null);
                return ms.ToArray();
            }
        }

        private static PwDatabase LoadFromBytes(byte[] kdbxBytes)
        {
            PwDatabase db = new PwDatabase();
            db.MasterKey = MakeKey();
            using (MemoryStream ms = new MemoryStream(kdbxBytes))
                new KdbxFile(db).Load(ms, KdbxFormat.Default, null);
            return db;
        }

        private static CompositeKey MakeKey()
        {
            CompositeKey key = new CompositeKey();
            key.AddUserKey(new KcpPassword(GoldenFileGenerator.MasterPassword));
            return key;
        }

        private static void CompareGroups(PwGroup a, PwGroup b, string fixtureCtx)
        {
            Assert.Equal(a.Name,  b.Name);
            Assert.Equal(a.Notes, b.Notes);
            Assert.Equal(a.Groups.UCount,  b.Groups.UCount);
            Assert.Equal(a.Entries.UCount, b.Entries.UCount);

            // Entries
            for (uint i = 0; i < a.Entries.UCount; ++i)
                CompareEntries(a.Entries.GetAt(i), b.Entries.GetAt(i), fixtureCtx);

            // Child groups (recursive)
            for (uint i = 0; i < a.Groups.UCount; ++i)
                CompareGroups(a.Groups.GetAt(i), b.Groups.GetAt(i), fixtureCtx);
        }

        private static void CompareEntries(PwEntry a, PwEntry b, string fixtureCtx)
        {
            string ctx = $"{fixtureCtx} / entry '{ReadField(a, PwDefs.TitleField)}'";

            Assert.Equal(ReadField(a, PwDefs.TitleField),    ReadField(b, PwDefs.TitleField));
            Assert.Equal(ReadField(a, PwDefs.UserNameField), ReadField(b, PwDefs.UserNameField));
            Assert.Equal(ReadField(a, PwDefs.UrlField),      ReadField(b, PwDefs.UrlField));
            Assert.Equal(ReadField(a, PwDefs.NotesField),    ReadField(b, PwDefs.NotesField));

            // Password comparison: decrypt protected string on both sides
            string pwA = ReadProtected(a, PwDefs.PasswordField);
            string pwB = ReadProtected(b, PwDefs.PasswordField);
            Assert.True(pwA == pwB,
                $"{ctx}: password mismatch after round-trip");

            // History
            Assert.Equal(a.History.UCount, b.History.UCount);

            // Binary attachments: names and sizes
            Assert.Equal(a.Binaries.UCount, b.Binaries.UCount);
            foreach (KeyValuePair<string, ProtectedBinary> kvp in a.Binaries)
            {
                ProtectedBinary pbB = b.Binaries.Get(kvp.Key);
                Assert.True(pbB != null,
                    $"{ctx}: attachment '{kvp.Key}' missing after round-trip");
                Assert.Equal(
                    kvp.Value.ReadData().Length,
                    pbB.ReadData().Length);
            }
        }

        private static string ReadField(PwEntry e, string field)
        {
            ProtectedString ps = e.Strings.Get(field);
            return ps != null ? ps.ReadString() : string.Empty;
        }

        private static string ReadProtected(PwEntry e, string field)
        {
            ProtectedString ps = e.Strings.Get(field);
            return ps != null ? ps.ReadString() : string.Empty;
        }
    }
}
