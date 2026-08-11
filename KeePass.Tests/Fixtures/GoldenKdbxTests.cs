using System;
using System.Collections.Generic;
using System.IO;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Xunit;

namespace KeePass.Tests.Fixtures
{
    /// <summary>
    /// Parameterized deserialization tests for the golden-file KDBX corpus.
    ///
    /// Each Theory opens one fixture from KeePass.Tests/Fixtures/GoldenKdbx/,
    /// deserializes it, and verifies the expected structural content.  If a
    /// fixture is missing it is generated automatically before the assertion runs.
    ///
    /// To force regeneration of all fixtures set the environment variable
    /// REGENERATE_GOLDEN_FILES=true, then run the RegenerateFixtures test.
    /// </summary>
    public class GoldenKdbxTests
    {
        // ── Fixture management ────────────────────────────────────────────────

        /// <summary>
        /// Regenerates all golden-file fixtures.  Run explicitly via
        ///   REGENERATE_GOLDEN_FILES=true dotnet test --filter RegenerateFixtures
        /// </summary>
        [Fact]
        public void RegenerateFixtures()
        {
            bool anyMissing = false;
            foreach (GoldenFileGenerator.FixtureSpec spec in GoldenFileGenerator.AllSpecs())
            {
                if (!File.Exists(Path.Combine(GoldenFileGenerator.FixtureDir, spec.Name)))
                {
                    anyMissing = true;
                    break;
                }
            }

            bool explicitRegen = string.Equals(
                Environment.GetEnvironmentVariable("REGENERATE_GOLDEN_FILES"),
                "true", StringComparison.OrdinalIgnoreCase);

            if (anyMissing || explicitRegen)
                GoldenFileGenerator.GenerateAll();
        }

        // ── Deserialization tests ─────────────────────────────────────────────

        public static IEnumerable<object[]> GetFixtures()
        {
            foreach (GoldenFileGenerator.FixtureSpec spec in GoldenFileGenerator.AllSpecs())
                yield return new object[] { spec.Name };
        }

        [Theory]
        [MemberData(nameof(GetFixtures))]
        public void GoldenFile_CanBeOpenedAndDeserialized(string fixtureName)
        {
            string path = Path.Combine(GoldenFileGenerator.FixtureDir, fixtureName);

            // Auto-generate if the fixture file was not committed / was deleted.
            if (!File.Exists(path))
                GoldenFileGenerator.GenerateAll();

            Assert.True(File.Exists(path),
                $"Fixture generation failed — file still missing: {path}");

            PwDatabase db = new PwDatabase();
            CompositeKey key = new CompositeKey();
            key.AddUserKey(new KcpPassword(GoldenFileGenerator.MasterPassword));
            db.MasterKey = key;

            using (FileStream fs = File.OpenRead(path))
            {
                KdbxFile kdbx = new KdbxFile(db);
                kdbx.Load(fs, KdbxFormat.Default, null);
            }

            // ── Structural assertions ─────────────────────────────────────────

            Assert.NotNull(db.RootGroup);
            Assert.True(db.RootGroup.Groups.UCount >= 2,
                $"{fixtureName}: expected ≥2 child groups, got {db.RootGroup.Groups.UCount}");

            uint totalEntries = CountEntries(db.RootGroup);
            Assert.True(totalEntries >= 5,
                $"{fixtureName}: expected ≥5 entries, got {totalEntries}");

            Assert.True(HasEntryWithHistory(db.RootGroup),
                $"{fixtureName}: expected at least one entry with history");

            Assert.True(HasEntryWithAttachment(db.RootGroup),
                $"{fixtureName}: expected at least one entry with attachment");

            Assert.True(db.CustomIcons.Count >= 1,
                $"{fixtureName}: expected ≥1 custom icon, got {db.CustomIcons.Count}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static uint CountEntries(PwGroup root)
        {
            uint total = 0;
            CountEntriesRec(root, ref total);
            return total;
        }

        private static void CountEntriesRec(PwGroup g, ref uint total)
        {
            total += g.Entries.UCount;
            for (uint i = 0; i < g.Groups.UCount; ++i)
                CountEntriesRec(g.Groups.GetAt(i), ref total);
        }

        private static bool HasEntryWithHistory(PwGroup root) =>
            AnyEntry(root, e => e.History.UCount > 0);

        private static bool HasEntryWithAttachment(PwGroup root) =>
            AnyEntry(root, e => e.Binaries.UCount > 0);

        private static bool AnyEntry(PwGroup root, Predicate<PwEntry> pred)
        {
            for (uint i = 0; i < root.Entries.UCount; ++i)
                if (pred(root.Entries.GetAt(i))) return true;
            for (uint i = 0; i < root.Groups.UCount; ++i)
                if (AnyEntry(root.Groups.GetAt(i), pred)) return true;
            return false;
        }
    }
}
