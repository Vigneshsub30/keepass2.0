using System.Collections.Generic;
using System.Linq;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using Xunit;

namespace KeePass.Tests.Domain
{
    /// <summary>
    /// Characterization tests for PwGroup — hierarchy management, recursive search,
    /// SearchEntries with various SearchParameters, and tag management.
    ///
    /// No file I/O: all tests operate on the in-memory object model.
    /// Tests are cross-platform.
    /// </summary>
    public class PwGroupTests
    {
        // ── 1. Group creation ─────────────────────────────────────────────────

        [Fact]
        public void Group_NewGroup_HasCorrectNameAndIcon()
        {
            PwGroup g = new PwGroup(true, true, "Finances", PwIcon.Money);
            Assert.Equal("Finances", g.Name);
            Assert.Equal(PwIcon.Money, g.IconId);
        }

        // ── 2. Child group management ─────────────────────────────────────────

        [Fact]
        public void Group_AddChild_ParentIsSet()
        {
            PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);
            PwGroup child = new PwGroup(true, true, "Child", PwIcon.Folder);
            root.AddGroup(child, true);

            Assert.Same(root, child.ParentGroup);
        }

        [Fact]
        public void Group_AddChild_ChildCountIncrements()
        {
            PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);
            root.AddGroup(new PwGroup(true, true, "A", PwIcon.Folder), true);
            root.AddGroup(new PwGroup(true, true, "B", PwIcon.Folder), true);

            Assert.Equal(2U, root.Groups.UCount);
        }

        [Fact]
        public void Group_RemoveChild_ChildCountDecrements()
        {
            PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);
            PwGroup child = new PwGroup(true, true, "ToRemove", PwIcon.Folder);
            root.AddGroup(child, true);

            root.Groups.Remove(child);
            Assert.Equal(0U, root.Groups.UCount);
        }

        // ── 3. Entry management ───────────────────────────────────────────────

        [Fact]
        public void Group_AddEntry_EntryCountIncrements()
        {
            PwGroup g = new PwGroup(true, true, "Root", PwIcon.Folder);
            g.AddEntry(MakeEntry("A"), true);
            g.AddEntry(MakeEntry("B"), true);

            Assert.Equal(2U, g.Entries.UCount);
        }

        [Fact]
        public void Group_RemoveEntry_EntryCountDecrements()
        {
            PwGroup g = new PwGroup(true, true, "Root", PwIcon.Folder);
            PwEntry e = MakeEntry("ToRemove");
            g.AddEntry(e, true);

            g.Entries.Remove(e);
            Assert.Equal(0U, g.Entries.UCount);
        }

        // ── 4. Recursive find ─────────────────────────────────────────────────

        [Fact]
        public void Group_FindGroup_SearchesRecursivelyByUuid()
        {
            PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);
            PwGroup level1 = new PwGroup(true, true, "L1", PwIcon.Folder);
            PwGroup level2 = new PwGroup(true, true, "L2", PwIcon.Folder);
            root.AddGroup(level1, true);
            level1.AddGroup(level2, true);

            PwGroup found = root.FindGroup(level2.Uuid, true);
            Assert.NotNull(found);
            Assert.Equal("L2", found.Name);
        }

        [Fact]
        public void Group_FindGroup_ReturnsSelfWhenMatching()
        {
            PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);
            PwGroup found = root.FindGroup(root.Uuid, true);
            Assert.Same(root, found);
        }

        [Fact]
        public void Group_FindEntry_SearchesRecursivelyByUuid()
        {
            PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);
            PwGroup sub = new PwGroup(true, true, "Sub", PwIcon.Folder);
            PwEntry e = MakeEntry("Deep");
            root.AddGroup(sub, true);
            sub.AddEntry(e, true);

            PwEntry found = root.FindEntry(e.Uuid, true);
            Assert.NotNull(found);
            Assert.Equal("Deep", found.Strings.Get(PwDefs.TitleField).ReadString());
        }

        // ── 5. SearchEntries ──────────────────────────────────────────────────

        [Fact]
        public void Group_SearchEntries_ByTitle_ReturnsMatches()
        {
            PwGroup root = BuildSearchableGroup();
            SearchParameters sp = new SearchParameters
            {
                SearchString = "Github",
                SearchInTitles = true,
                SearchInUserNames = false,
                SearchInNotes = false
            };
            PwObjectList<PwEntry> results = new PwObjectList<PwEntry>();
            root.SearchEntries(sp, results);

            Assert.Equal(1U, results.UCount);
            Assert.Equal("Github", results.GetAt(0).Strings.Get(PwDefs.TitleField).ReadString());
        }

        [Fact]
        public void Group_SearchEntries_ByUsername_ReturnsMatches()
        {
            PwGroup root = BuildSearchableGroup();
            SearchParameters sp = new SearchParameters
            {
                SearchString = "alice@example.com",
                SearchInTitles = false,
                SearchInUserNames = true,
                SearchInNotes = false
            };
            PwObjectList<PwEntry> results = new PwObjectList<PwEntry>();
            root.SearchEntries(sp, results);

            Assert.Equal(1U, results.UCount);
        }

        [Fact]
        public void Group_SearchEntries_EmptyString_ReturnsAll()
        {
            PwGroup root = BuildSearchableGroup();
            SearchParameters sp = new SearchParameters
            {
                SearchString = string.Empty
            };
            PwObjectList<PwEntry> results = new PwObjectList<PwEntry>();
            root.SearchEntries(sp, results);

            // All entries match an empty search string
            Assert.True(results.UCount >= 2);
        }

        [Fact]
        public void Group_SearchEntries_NoMatchReturnsEmpty()
        {
            PwGroup root = BuildSearchableGroup();
            SearchParameters sp = new SearchParameters
            {
                SearchString = "ZZZ-no-match-ZZZ",
                SearchInTitles = true,
                SearchInUserNames = true,
                SearchInNotes = true
            };
            PwObjectList<PwEntry> results = new PwObjectList<PwEntry>();
            root.SearchEntries(sp, results);

            Assert.Equal(0U, results.UCount);
        }

        // ── 6. Tag management ─────────────────────────────────────────────────

        [Fact]
        public void Group_Tags_AddAndRetrieve()
        {
            PwGroup g = new PwGroup(true, true, "Work", PwIcon.Folder);
            g.Tags.Add("work");
            g.Tags.Add("important");

            Assert.Contains("work",      g.Tags);
            Assert.Contains("important", g.Tags);
            Assert.Equal(2, g.Tags.Count);
        }

        [Fact]
        public void Group_Tags_Remove_DecrementCount()
        {
            PwGroup g = new PwGroup(true, true, "Root", PwIcon.Folder);
            g.Tags.Add("alpha");
            g.Tags.Add("beta");
            g.Tags.Remove("alpha");

            Assert.Equal(1, g.Tags.Count);
            Assert.DoesNotContain("alpha", g.Tags);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static PwEntry MakeEntry(string title,
                                          string user  = "user",
                                          string notes = "")
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, title));
            e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, user));
            e.Strings.Set(PwDefs.NotesField,    new ProtectedString(false, notes));
            return e;
        }

        /// <summary>
        /// Builds a group tree with two distinct entries for search tests.
        /// </summary>
        private static PwGroup BuildSearchableGroup()
        {
            PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);

            PwEntry e1 = MakeEntry("Github", "alice@example.com", "Version control");
            PwEntry e2 = MakeEntry("Jira",   "bob@corp.com",      "Issue tracker");

            root.AddEntry(e1, true);
            root.AddEntry(e2, true);

            return root;
        }
    }
}
