using System;
using System.Text;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using Xunit;

namespace KeePass.Tests.Domain
{
    /// <summary>
    /// Characterization tests for PwEntry — field access, binary attachments,
    /// auto-type configuration, history management, CloneDeep, and tags.
    ///
    /// No file I/O: all tests operate on the in-memory object model.
    /// Tests are cross-platform.
    /// </summary>
    public class PwEntryTests
    {
        // ── 1. Standard field access ─────────────────────────────────────────

        [Fact]
        public void Entry_StandardFields_SetAndGetViaProtectedString()
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "MyTitle"));
            e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "alice"));
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "s3cret!"));
            e.Strings.Set(PwDefs.UrlField,      new ProtectedString(false, "https://example.com"));
            e.Strings.Set(PwDefs.NotesField,    new ProtectedString(false, "Some notes."));

            Assert.Equal("MyTitle",            e.Strings.Get(PwDefs.TitleField).ReadString());
            Assert.Equal("alice",              e.Strings.Get(PwDefs.UserNameField).ReadString());
            Assert.Equal("s3cret!",            e.Strings.Get(PwDefs.PasswordField).ReadString());
            Assert.Equal("https://example.com",e.Strings.Get(PwDefs.UrlField).ReadString());
            Assert.Equal("Some notes.",        e.Strings.Get(PwDefs.NotesField).ReadString());
        }

        [Fact]
        public void Entry_PasswordField_IsProtectedByDefault()
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "secret"));

            ProtectedString ps = e.Strings.Get(PwDefs.PasswordField);
            Assert.True(ps.IsProtected);
        }

        // ── 2. Custom string fields ──────────────────────────────────────────

        [Fact]
        public void Entry_CustomStringField_AddAndRetrieve()
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set("AccountNumber", new ProtectedString(false, "1234-5678"));

            ProtectedString ps = e.Strings.Get("AccountNumber");
            Assert.NotNull(ps);
            Assert.Equal("1234-5678", ps.ReadString());
        }

        [Fact]
        public void Entry_CustomStringField_Remove()
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set("Temp", new ProtectedString(false, "value"));
            e.Strings.Remove("Temp");

            Assert.Null(e.Strings.Get("Temp"));
        }

        // ── 3. Binary attachments ────────────────────────────────────────────

        [Fact]
        public void Entry_Binaries_AddAndRetrieveByKey()
        {
            PwEntry e = new PwEntry(true, true);
            byte[] content = Encoding.UTF8.GetBytes("File content here.");
            e.Binaries.Set("readme.txt", new ProtectedBinary(false, content));

            Assert.Equal(1U, e.Binaries.UCount);

            ProtectedBinary pb = e.Binaries.Get("readme.txt");
            Assert.NotNull(pb);
            Assert.Equal(content.Length, pb.ReadData().Length);
        }

        [Fact]
        public void Entry_Binaries_Remove_DecrementsCount()
        {
            PwEntry e = new PwEntry(true, true);
            e.Binaries.Set("a.txt", new ProtectedBinary(false, new byte[] { 1, 2, 3 }));
            e.Binaries.Set("b.txt", new ProtectedBinary(false, new byte[] { 4, 5, 6 }));

            Assert.Equal(2U, e.Binaries.UCount);
            e.Binaries.Remove("a.txt");
            Assert.Equal(1U, e.Binaries.UCount);
            Assert.Null(e.Binaries.Get("a.txt"));
        }

        // ── 4. Auto-type configuration ───────────────────────────────────────

        [Fact]
        public void Entry_AutoType_DefaultSequenceRoundTrips()
        {
            PwEntry e = new PwEntry(true, true);
            e.AutoType.DefaultSequence = "{USERNAME}{TAB}{PASSWORD}{ENTER}";

            Assert.Equal("{USERNAME}{TAB}{PASSWORD}{ENTER}", e.AutoType.DefaultSequence);
        }

        [Fact]
        public void Entry_AutoType_AddAssociation_IsRetrievable()
        {
            PwEntry e = new PwEntry(true, true);
            e.AutoType.Add(new AutoTypeAssociation("*MyApp*", "{PASSWORD}{ENTER}"));

            Assert.Equal(1, e.AutoType.AssociationsCount);

            // Retrieve via IEnumerable
            foreach (AutoTypeAssociation a in e.AutoType.Associations)
            {
                Assert.Equal("*MyApp*",         a.WindowName);
                Assert.Equal("{PASSWORD}{ENTER}", a.Sequence);
            }
        }

        [Fact]
        public void Entry_AutoType_EnabledByDefault()
        {
            PwEntry e = new PwEntry(true, true);
            Assert.True(e.AutoType.Enabled);
        }

        // ── 5. History management ────────────────────────────────────────────

        [Fact]
        public void Entry_History_CreateBackupIncrementsCount()
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "v1"));

            e.CreateBackup(null);
            Assert.Equal(1U, e.History.UCount);

            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "v2"));
            e.CreateBackup(null);
            Assert.Equal(2U, e.History.UCount);
        }

        [Fact]
        public void Entry_History_RestoreFromBackup_RestoresFields()
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Original"));
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "original-pw"));

            e.CreateBackup(null);

            // Modify the entry
            e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Modified"));
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "modified-pw"));

            // Restore backup index 0 (the original state)
            e.RestoreFromBackup(0);

            Assert.Equal("Original",    e.Strings.Get(PwDefs.TitleField).ReadString());
            Assert.Equal("original-pw", e.Strings.Get(PwDefs.PasswordField).ReadString());
        }

        [Fact]
        public void Entry_History_MaintainBackups_TrimsToBound()
        {
            PwDatabase db = new PwDatabase();
            db.MaintenanceHistoryDays = 365;

            PwEntry e = new PwEntry(true, true);
            db.RootGroup = new PwGroup(true, true, "Root", PwIcon.Folder);
            db.RootGroup.AddEntry(e, true);

            // Create 15 backups
            for (int i = 0; i < 15; ++i)
            {
                e.Strings.Set(PwDefs.PasswordField,
                    new ProtectedString(true, $"password-{i}"));
                e.CreateBackup(null);
            }

            Assert.True(e.History.UCount >= 15);

            // MaintainBackups trims based on MaxHistoryItems (default 10 in PwDatabase)
            e.MaintainBackups(db);

            // Default MaxHistoryItems is 10 (from PwDatabase.DefaultHistoryMaxItems)
            Assert.True(e.History.UCount <= 10,
                $"Expected ≤10 history items after trim, got {e.History.UCount}");
        }

        // ── 6. CloneDeep ─────────────────────────────────────────────────────

        [Fact]
        public void Entry_CloneDeep_ProducesIndependentCopy()
        {
            PwEntry original = new PwEntry(true, true);
            original.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Orig"));
            original.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "pw-orig"));
            original.Binaries.Set("file.txt",
                new ProtectedBinary(false, Encoding.UTF8.GetBytes("hello")));
            original.AutoType.DefaultSequence = "{PASSWORD}";

            PwEntry clone = original.CloneDeep();

            // Modify the clone
            clone.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Clone"));
            clone.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "pw-clone"));

            // Original must be unchanged
            Assert.Equal("Orig",    original.Strings.Get(PwDefs.TitleField).ReadString());
            Assert.Equal("pw-orig", original.Strings.Get(PwDefs.PasswordField).ReadString());
            Assert.Equal("Clone",   clone.Strings.Get(PwDefs.TitleField).ReadString());
        }

        [Fact]
        public void Entry_CloneDeep_HasDifferentUuid()
        {
            PwEntry original = new PwEntry(true, true);
            PwEntry clone = original.CloneDeep();

            // CloneDeep assigns a new UUID to the clone
            Assert.False(original.Uuid.Equals(clone.Uuid));
        }

        // ── 7. Tags ──────────────────────────────────────────────────────────

        [Fact]
        public void Entry_Tags_AddAndRetrieve()
        {
            PwEntry e = new PwEntry(true, true);
            e.Tags.Add("finance");
            e.Tags.Add("work");

            Assert.Contains("finance", e.Tags);
            Assert.Contains("work",    e.Tags);
            Assert.Equal(2, e.Tags.Count);
        }

        [Fact]
        public void Entry_Tags_Remove_DecrementCount()
        {
            PwEntry e = new PwEntry(true, true);
            e.Tags.Add("tag1");
            e.Tags.Add("tag2");
            e.Tags.Remove("tag1");

            Assert.Equal(1, e.Tags.Count);
            Assert.DoesNotContain("tag1", e.Tags);
        }
    }
}
