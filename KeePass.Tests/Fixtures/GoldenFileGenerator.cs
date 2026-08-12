using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using KeePassLib;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

namespace KeePass.Tests.Fixtures
{
    /// <summary>
    /// Generates the golden-file KDBX corpus used by GoldenKdbxTests.
    /// Each file encodes one (cipher × KDF × compression × format-version)
    /// combination and contains a fixed set of groups, entries, attachments,
    /// history, and custom icons.
    ///
    /// Master password for all fixtures: "GoldenTestPassword1!"
    /// </summary>
    internal static class GoldenFileGenerator
    {
        internal const string MasterPassword = "GoldenTestPassword1!";

        // ChaCha20 cipher UUID (well-known constant from KeePass format spec)
        private static readonly PwUuid s_chaCha20Uuid = new PwUuid(new byte[]
        {
            0xD6, 0x03, 0x8A, 0x2B, 0x8B, 0x6F, 0x4C, 0xB5,
            0xA5, 0x24, 0x33, 0x9A, 0x31, 0xDB, 0xB5, 0x9A
        });

        // Minimal 1×1 blue PNG (67 bytes, no System.Drawing dependency)
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

        internal static readonly string FixtureDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "Fixtures", "GoldenKdbx"));

        // ── Fixture matrix ────────────────────────────────────────────────────

        internal record FixtureSpec(
            string Name,
            PwUuid CipherUuid,
            string CipherName,
            string KdfName,
            PwCompressionAlgorithm Compression,
            bool Include41Features);

        internal static IEnumerable<FixtureSpec> AllSpecs()
        {
            PwUuid aesUuid = StandardAesEngine.AesUuid;

            string[] kdfNames = { "AesKdf", "Argon2d", "Argon2id" };
            PwCompressionAlgorithm[] compressions =
            {
                PwCompressionAlgorithm.None,
                PwCompressionAlgorithm.GZip
            };
            string[] cipherNames = { "AES", "ChaCha20" };
            PwUuid[] cipherUuids = { aesUuid, s_chaCha20Uuid };

            for (int ci = 0; ci < cipherNames.Length; ++ci)
            for (int ki = 0; ki < kdfNames.Length; ++ki)
            for (int pi = 0; pi < compressions.Length; ++pi)
            {
                string cName = cipherNames[ci].ToLower();
                string kName = kdfNames[ki].ToLower();
                string pName = compressions[pi] == PwCompressionAlgorithm.None
                    ? "none" : "gzip";

                yield return new FixtureSpec(
                    $"kdbx40-{cName}-{kName}-{pName}.kdbx",
                    cipherUuids[ci],
                    cipherNames[ci],
                    kdfNames[ki],
                    compressions[pi],
                    Include41Features: false);
            }

            // One KDBX 4.1 fixture (group tags + named custom icon trigger 4.1)
            yield return new FixtureSpec(
                "kdbx41-aes-argon2id-gzip.kdbx",
                aesUuid,
                "AES",
                "Argon2id",
                PwCompressionAlgorithm.GZip,
                Include41Features: true);
        }

        // ── Generator entry point ─────────────────────────────────────────────

        internal static void GenerateAll()
        {
            Directory.CreateDirectory(FixtureDir);

            var manifest = new List<ManifestEntry>();

            foreach (FixtureSpec spec in AllSpecs())
            {
                string path = Path.Combine(FixtureDir, spec.Name);
                byte[] kdbxBytes = GenerateFixture(spec);
                File.WriteAllBytes(path, kdbxBytes);

                manifest.Add(new ManifestEntry
                {
                    File = spec.Name,
                    Cipher = spec.CipherName,
                    Kdf = spec.KdfName,
                    Compression = spec.Compression.ToString(),
                    Features41 = spec.Include41Features,
                    MasterPassword = MasterPassword,
                    Groups = 3,
                    Entries = 5,
                    HasAttachment = true,
                    HasHistory = true,
                    HasCustomIcon = true
                });
            }

            string manifestPath = Path.Combine(FixtureDir, "manifest.json");
            JsonSerializerOptions opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(manifestPath,
                JsonSerializer.Serialize(manifest, opts),
                new UTF8Encoding(false));
        }

        // ── Single-fixture generation ─────────────────────────────────────────

        private static byte[] GenerateFixture(FixtureSpec spec)
        {
            PwDatabase db = new PwDatabase();
            db.DataCipherUuid = spec.CipherUuid;
            db.Compression = spec.Compression;
            db.KdfParameters = BuildKdfParameters(spec.KdfName);
            db.Name = $"GoldenKdbx-{spec.Name}";
            db.Description = "Golden-file fixture generated by WO-016.";

            // Build group hierarchy ──────────────────────────────────────────

            PwGroup root   = new PwGroup(true, true, "Root",   PwIcon.Folder);
            PwGroup social = new PwGroup(true, true, "Social", PwIcon.Folder);
            PwGroup work   = new PwGroup(true, true, "Work",   PwIcon.Folder);
            db.RootGroup = root;
            root.AddGroup(social, true);
            root.AddGroup(work, true);

            if (spec.Include41Features)
            {
                social.Tags.Add("social"); // triggers KDBX 4.1
                work.Tags.Add("work");
            }

            // 5 entries ──────────────────────────────────────────────────────
            AddEntry(root,   "GitHub",     "alice", "gh-password!");
            AddEntry(social, "Twitter",    "alice", "tw-password!");
            AddEntry(social, "LinkedIn",   "alice", "li-password!");
            AddEntry(work,   "Jira",       "alice", "jira-password!");

            // Entry with attachment
            PwEntry entryWithAttachment = MakeEntry("Attachments", "alice", "att-password!");
            entryWithAttachment.Binaries.Set("readme.txt",
                new ProtectedBinary(false, Encoding.UTF8.GetBytes("Attachment content.")));
            root.AddEntry(entryWithAttachment, true);

            // Create history on entry 0 (2 previous versions)
            PwEntry historyEntry = root.Entries.GetAt(0);
            historyEntry.CreateBackup(null);
            historyEntry.Strings.Set(PwDefs.PasswordField,
                new ProtectedString(true, "updated-password!"));
            historyEntry.CreateBackup(null);
            historyEntry.Strings.Set(PwDefs.PasswordField,
                new ProtectedString(true, "final-password!"));

            // Custom icon ────────────────────────────────────────────────────
            PwUuid iconUuid = new PwUuid(true);
            PwCustomIcon icon = new PwCustomIcon(iconUuid, s_minimalPng);
            if (spec.Include41Features)
            {
                icon.Name = "GoldenIcon";  // triggers KDBX 4.1
                icon.LastModificationTime = new DateTime(
                    2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            db.CustomIcons.Add(icon);

            // Serialise ──────────────────────────────────────────────────────
            using (MemoryStream ms = new MemoryStream())
            {
                new KdbxFile(db).Save(ms, null, KdbxFormat.Default, null);
                return ms.ToArray();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AddEntry(PwGroup group, string title,
            string user, string password)
        {
            group.AddEntry(MakeEntry(title, user, password), true);
        }

        private static PwEntry MakeEntry(string title, string user, string password)
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.TitleField,
                new ProtectedString(false, title));
            e.Strings.Set(PwDefs.UserNameField,
                new ProtectedString(false, user));
            e.Strings.Set(PwDefs.PasswordField,
                new ProtectedString(true, password));
            e.Strings.Set(PwDefs.UrlField,
                new ProtectedString(false, $"https://{title.ToLower()}.example.com"));
            e.Strings.Set(PwDefs.NotesField,
                new ProtectedString(false, $"Notes for {title}."));
            return e;
        }

        private static KdfParameters BuildKdfParameters(string kdfName)
        {
            switch (kdfName)
            {
                case "AesKdf":
                {
                    AesKdf kdf = new AesKdf();
                    KdfParameters p = kdf.GetDefaultParameters();
                    p.SetUInt64(AesKdf.ParamRounds, 6000);
                    return p;
                }
                case "Argon2d":
                {
                    Argon2Kdf kdf = new Argon2Kdf(Argon2Type.D);
                    KdfParameters p = kdf.GetDefaultParameters();
                    kdf.Randomize(p);
                    p.SetUInt64(Argon2Kdf.ParamMemory, 8 * 1024);  // 8 MB — fast for tests
                    p.SetUInt64(Argon2Kdf.ParamIterations, 1);
                    p.SetUInt32(Argon2Kdf.ParamParallelism, 1);
                    return p;
                }
                case "Argon2id":
                {
                    Argon2Kdf kdf = new Argon2Kdf(Argon2Type.ID);
                    KdfParameters p = kdf.GetDefaultParameters();
                    kdf.Randomize(p);
                    p.SetUInt64(Argon2Kdf.ParamMemory, 8 * 1024);
                    p.SetUInt64(Argon2Kdf.ParamIterations, 1);
                    p.SetUInt32(Argon2Kdf.ParamParallelism, 1);
                    return p;
                }
                default:
                    throw new ArgumentException($"Unknown KDF: {kdfName}");
            }
        }

        // Used for manifest serialisation (public properties required by JsonSerializer)
        private sealed class ManifestEntry
        {
            public string File { get; set; }
            public string Cipher { get; set; }
            public string Kdf { get; set; }
            public string Compression { get; set; }
            public bool Features41 { get; set; }
            public string MasterPassword { get; set; }
            public int Groups { get; set; }
            public int Entries { get; set; }
            public bool HasAttachment { get; set; }
            public bool HasHistory { get; set; }
            public bool HasCustomIcon { get; set; }
        }
    }
}
