using System;
using System.IO;
using System.Reflection;
using KeePassLib;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Xunit;

namespace KeePass.Tests.Serialization
{
    /// <summary>
    /// Tests for KdbxFile.GetMinKdbxVersion() — the internal method that inspects
    /// the database's content and returns the minimum KDBX format version needed
    /// to represent it without data loss.
    ///
    /// Strategy: create a minimal database, add a single triggering feature,
    /// save to a MemoryStream, and parse the resulting format version from the
    /// binary header using KdbxHeaderParser.  Each test is fully independent.
    ///
    /// KDBX version mapping (fileVersion uint32, little-endian):
    ///   0x00040000 → 4.0   (no 4.1 triggers)
    ///   0x00040001 → 4.1   (group tags, QualityCheck=false, named/timed custom icon,
    ///                        custom data with timestamp)
    /// </summary>
    public class KdbxVersionSelectionTests
    {
        // ── Constant expected versions ─────────────────────────────────────────
        private const ushort MajorV4 = 4;
        private const ushort MinorV40 = 0;  // 4.0
        private const ushort MinorV41 = 1;  // 4.1

        // ── Test 1: Baseline — no 4.1 triggers → KDBX 4.0 ──────────────────

        [Fact]
        public void NoTriggers_ProducesKdbx40()
        {
            PwDatabase db = CreateMinimalDb();
            // No 4.1-triggering features added.
            KdbxHeaderInfo h = SaveAndParseHeader(db);

            Assert.Equal(MajorV4,   h.MajorVersion);
            Assert.Equal(MinorV40,  h.MinorVersion);
        }

        // ── Test 2: Group tags → KDBX 4.1 ─────────────────────────────────

        [Fact]
        public void GroupWithTag_ProducesKdbx41()
        {
            PwDatabase db = CreateMinimalDb();
            db.RootGroup.Tags.Add("work");

            KdbxHeaderInfo h = SaveAndParseHeader(db);

            Assert.Equal(MajorV4,  h.MajorVersion);
            Assert.Equal(MinorV41, h.MinorVersion);
        }

        // ── Test 3: Entry QualityCheck=false → KDBX 4.1 ───────────────────

        [Fact]
        public void EntryWithQualityCheckFalse_ProducesKdbx41()
        {
            PwDatabase db = CreateMinimalDb();

            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Test"));
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "pass"));
            e.QualityCheck = false;  // triggers KDBX 4.1
            db.RootGroup.AddEntry(e, true);

            KdbxHeaderInfo h = SaveAndParseHeader(db);

            Assert.Equal(MajorV4,  h.MajorVersion);
            Assert.Equal(MinorV41, h.MinorVersion);
        }

        // ── Test 4: Custom icon with Name → KDBX 4.1 ──────────────────────

        [Fact]
        public void CustomIconWithName_ProducesKdbx41()
        {
            PwDatabase db = CreateMinimalDb();

            PwCustomIcon icon = new PwCustomIcon(new PwUuid(true), s_minimalPng);
            icon.Name = "MyIcon";  // triggers KDBX 4.1
            db.CustomIcons.Add(icon);

            KdbxHeaderInfo h = SaveAndParseHeader(db);

            Assert.Equal(MajorV4,  h.MajorVersion);
            Assert.Equal(MinorV41, h.MinorVersion);
        }

        // ── Test 5: Custom icon with LastModificationTime → KDBX 4.1 ───────

        [Fact]
        public void CustomIconWithLastModTime_ProducesKdbx41()
        {
            PwDatabase db = CreateMinimalDb();

            PwCustomIcon icon = new PwCustomIcon(new PwUuid(true), s_minimalPng);
            icon.LastModificationTime =
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);  // triggers KDBX 4.1
            db.CustomIcons.Add(icon);

            KdbxHeaderInfo h = SaveAndParseHeader(db);

            Assert.Equal(MajorV4,  h.MajorVersion);
            Assert.Equal(MinorV41, h.MinorVersion);
        }

        // ── Test 6: Custom data with timestamp → KDBX 4.1 ──────────────────

        /// <summary>
        /// StringDictionaryEx.Set(key, value, DateTime?) is internal.
        /// Access it via reflection to set a per-entry modification timestamp,
        /// which triggers the KDBX 4.1 version bump in GetMinKdbxVersion().
        /// </summary>
        [Fact]
        public void CustomDataWithTimestamp_ProducesKdbx41()
        {
            PwDatabase db = CreateMinimalDb();

            // Call internal StringDictionaryEx.Set(string, string, DateTime?) via reflection.
            MethodInfo setWithTimestamp = db.CustomData.GetType().GetMethod(
                "Set",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(string), typeof(DateTime?) },
                null);

            Assert.True(setWithTimestamp != null,
                "StringDictionaryEx.Set(string, string, DateTime?) not found via reflection. " +
                "Has the internal API changed?");

            DateTime ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            setWithTimestamp.Invoke(db.CustomData,
                new object[] { "wo018-key", "wo018-value", (DateTime?)ts });

            KdbxHeaderInfo h = SaveAndParseHeader(db);

            Assert.Equal(MajorV4,  h.MajorVersion);
            Assert.Equal(MinorV41, h.MinorVersion);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a minimal PwDatabase with a master key and Argon2id KDF.
        /// No 4.1-triggering features are present.
        /// </summary>
        private static PwDatabase CreateMinimalDb()
        {
            PwDatabase db = new PwDatabase();
            CompositeKey key = new CompositeKey();
            key.AddUserKey(new KcpPassword("WO018-TestPassword!"));
            db.MasterKey = key;

            db.DataCipherUuid = StandardAesEngine.AesUuid;
            db.Compression = PwCompressionAlgorithm.GZip;

            Argon2Kdf kdf = new Argon2Kdf(Argon2Type.ID);
            KdfParameters p = kdf.GetDefaultParameters();
            kdf.Randomize(p);
            p.SetUInt64(Argon2Kdf.ParamMemory,      4 * 1024);  // 4 MB — minimal for tests
            p.SetUInt64(Argon2Kdf.ParamIterations,  1);
            p.SetUInt32(Argon2Kdf.ParamParallelism, 1);
            db.KdfParameters = p;

            db.RootGroup = new PwGroup(true, true, "Root", PwIcon.Folder);
            return db;
        }

        /// <summary>
        /// Saves the database to a MemoryStream and parses the binary header
        /// to extract the format version selected by KdbxFile.
        /// </summary>
        private static KdbxHeaderInfo SaveAndParseHeader(PwDatabase db)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new KdbxFile(db).Save(ms, null, KdbxFormat.Default, null);
                return KdbxHeaderParser.Parse(ms.ToArray());
            }
        }

        // Minimal 1×1 blue PNG — no System.Drawing dependency
        private static readonly byte[] s_minimalPng = new byte[]
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
