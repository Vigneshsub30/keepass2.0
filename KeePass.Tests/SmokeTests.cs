using KeePassLib;
using KeePassLib.Security;
using Xunit;

namespace KeePass.Tests
{
    /// <summary>
    /// Smoke tests for the KeePassLib domain model.
    /// Each test is designed to be minimal: it proves that the in-memory object
    /// graph works correctly and that the test infrastructure is wired up, not
    /// that any persistence or cryptographic operation succeeds.
    /// </summary>
    public class SmokeTests
    {
        /// <summary>
        /// Verifies that a <see cref="PwGroup"/> can be added to the root group
        /// of a new <see cref="PwDatabase"/> and retrieved by index.
        /// </summary>
        [Fact]
        public void PwDatabase_AddGroup_RetrievableByIndex()
        {
            var db = new PwDatabase();
            var root = new PwGroup(true, true, "Root", PwIcon.Folder);
            db.RootGroup = root;

            var group = new PwGroup(true, true, "Test Group", PwIcon.FolderOpen);
            root.AddGroup(group, true);

            Assert.Equal(1u, root.Groups.UCount);
            Assert.Equal("Test Group", root.Groups.GetAt(0).Name);
        }

        /// <summary>
        /// Verifies that a <see cref="PwEntry"/> can be added to a group and
        /// that its string fields survive a round-trip through the in-memory
        /// object graph.
        /// </summary>
        [Fact]
        public void PwGroup_AddEntry_TitleFieldRoundTrip()
        {
            var group = new PwGroup(true, true, "Group", PwIcon.Folder);
            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "My Password Entry"));
            entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "alice"));

            group.AddEntry(entry, true);

            Assert.Equal(1u, group.Entries.UCount);
            PwEntry stored = group.Entries.GetAt(0);
            Assert.Equal("My Password Entry", stored.Strings.ReadSafe(PwDefs.TitleField));
            Assert.Equal("alice", stored.Strings.ReadSafe(PwDefs.UserNameField));
        }

        /// <summary>
        /// End-to-end smoke test: builds a small in-memory database hierarchy
        /// (root → subgroup → entry) and asserts the full structure is intact.
        /// </summary>
        [Fact]
        public void PwDatabase_GroupAndEntryHierarchy_IntactAfterBuild()
        {
            var db = new PwDatabase();
            var root = new PwGroup(true, true, "Root", PwIcon.Folder);
            db.RootGroup = root;

            var subGroup = new PwGroup(true, true, "Social", PwIcon.Folder);
            root.AddGroup(subGroup, true);

            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "GitHub"));
            entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "bob"));
            subGroup.AddEntry(entry, true);

            // Root has exactly one child group
            Assert.Equal(1u, root.Groups.UCount);

            // The child group has exactly one entry
            PwGroup social = root.Groups.GetAt(0);
            Assert.Equal("Social", social.Name);
            Assert.Equal(1u, social.Entries.UCount);

            // The entry fields are correct
            PwEntry stored = social.Entries.GetAt(0);
            Assert.Equal("GitHub", stored.Strings.ReadSafe(PwDefs.TitleField));
            Assert.Equal("bob", stored.Strings.ReadSafe(PwDefs.UserNameField));
        }
    }
}
