using System;
using System.Collections.Generic;
using KeePassLib;
using KeePassLib.Security;
using Xunit;

namespace KeePass.Tests.Domain
{
    /// <summary>
    /// Characterization tests for PwDatabase — the root domain object.
    /// Covers: construction, group/entry CRUD, MergeIn strategies, deleted-object
    /// tracking, custom icon management, and metadata properties.
    ///
    /// No file I/O: all tests operate on the in-memory object model.
    /// Tests are cross-platform (no Windows-specific APIs are required).
    /// </summary>
    public class PwDatabaseTests
    {
        // ── 1. Construction and initial state ────────────────────────────────

        [Fact]
        public void NewDatabase_DefaultCipherIsAes()
        {
            PwDatabase db = MakeDb();
            // Default cipher is AES-256 (StandardAesEngine.AesUuid)
            Assert.False(db.DataCipherUuid.Equals(PwUuid.Zero));
        }

        [Fact]
        public void NewDatabase_KdfParametersNotNull()
        {
            PwDatabase db = MakeDb();
            Assert.NotNull(db.KdfParameters);
        }

        [Fact]
        public void NewDatabase_RootGroupNotNull()
        {
            PwDatabase db = MakeDb();
            Assert.NotNull(db.RootGroup);
        }

        [Fact]
        public void NewDatabase_DeletedObjectsIsEmpty()
        {
            PwDatabase db = MakeDb();
            Assert.Equal(0U, db.DeletedObjects.UCount);
        }

        // ── 2. Metadata properties ────────────────────────────────────────────

        [Fact]
        public void Metadata_NameRoundTrips()
        {
            PwDatabase db = MakeDb();
            db.Name = "My Vault";
            Assert.Equal("My Vault", db.Name);
        }

        [Fact]
        public void Metadata_DescriptionRoundTrips()
        {
            PwDatabase db = MakeDb();
            db.Description = "Test description";
            Assert.Equal("Test description", db.Description);
        }

        [Fact]
        public void Metadata_DefaultUserNameRoundTrips()
        {
            PwDatabase db = MakeDb();
            db.DefaultUserName = "alice";
            Assert.Equal("alice", db.DefaultUserName);
        }

        [Fact]
        public void Metadata_MaintenanceHistoryDaysRoundTrips()
        {
            PwDatabase db = MakeDb();
            db.MaintenanceHistoryDays = 90;
            Assert.Equal(90U, db.MaintenanceHistoryDays);
        }

        // ── 3. Group hierarchy operations ────────────────────────────────────

        [Fact]
        public void Group_AddChild_IsFoundRecursively()
        {
            PwDatabase db = MakeDb();
            PwGroup child = new PwGroup(true, true, "Work", PwIcon.Folder);
            db.RootGroup.AddGroup(child, true);

            PwGroup found = db.RootGroup.FindGroup(child.Uuid, true);
            Assert.NotNull(found);
            Assert.Equal("Work", found.Name);
        }

        [Fact]
        public void Group_AddNestedChild_IsFoundRecursively()
        {
            PwDatabase db = MakeDb();
            PwGroup level1 = new PwGroup(true, true, "Level1", PwIcon.Folder);
            PwGroup level2 = new PwGroup(true, true, "Level2", PwIcon.Folder);
            db.RootGroup.AddGroup(level1, true);
            level1.AddGroup(level2, true);

            PwGroup found = db.RootGroup.FindGroup(level2.Uuid, true);
            Assert.NotNull(found);
            Assert.Equal("Level2", found.Name);
        }

        [Fact]
        public void Group_Remove_IsNoLongerFound()
        {
            PwDatabase db = MakeDb();
            PwGroup child = new PwGroup(true, true, "ToRemove", PwIcon.Folder);
            db.RootGroup.AddGroup(child, true);

            Assert.Equal(1U, db.RootGroup.Groups.UCount);
            db.RootGroup.Groups.Remove(child);
            Assert.Equal(0U, db.RootGroup.Groups.UCount);
            Assert.Null(db.RootGroup.FindGroup(child.Uuid, true));
        }

        // ── 4. Entry operations ───────────────────────────────────────────────

        [Fact]
        public void Entry_AddToGroup_IsFoundByUuid()
        {
            PwDatabase db = MakeDb();
            PwEntry e = MakeEntry("GitHub", "alice", "secret");
            db.RootGroup.AddEntry(e, true);

            PwEntry found = db.RootGroup.FindEntry(e.Uuid, true);
            Assert.NotNull(found);
            Assert.Equal("GitHub", found.Strings.Get(PwDefs.TitleField).ReadString());
        }

        [Fact]
        public void Entry_PasswordField_CanBeReadViaProtectedString()
        {
            PwDatabase db = MakeDb();
            PwEntry e = MakeEntry("Test", "user", "MyP@ssword!");
            db.RootGroup.AddEntry(e, true);

            PwEntry found = db.RootGroup.FindEntry(e.Uuid, true);
            string pw = found.Strings.Get(PwDefs.PasswordField).ReadString();
            Assert.Equal("MyP@ssword!", pw);
        }

        [Fact]
        public void Entry_Remove_IsNoLongerFound()
        {
            PwDatabase db = MakeDb();
            PwEntry e = MakeEntry("ToRemove", "u", "p");
            db.RootGroup.AddEntry(e, true);

            db.RootGroup.Entries.Remove(e);

            Assert.Null(db.RootGroup.FindEntry(e.Uuid, true));
        }

        [Fact]
        public void Entry_FindAcrossSubGroups()
        {
            PwDatabase db = MakeDb();
            PwGroup sub = new PwGroup(true, true, "Sub", PwIcon.Folder);
            db.RootGroup.AddGroup(sub, true);
            PwEntry e = MakeEntry("Deep", "u", "p");
            sub.AddEntry(e, true);

            PwEntry found = db.RootGroup.FindEntry(e.Uuid, true);
            Assert.NotNull(found);
        }

        // ── 5. Entry history ─────────────────────────────────────────────────

        [Fact]
        public void Entry_History_BackupPreservesContent()
        {
            PwDatabase db = MakeDb();
            PwEntry e = MakeEntry("WithHistory", "user", "original-pw");
            db.RootGroup.AddEntry(e, true);

            e.CreateBackup(null);
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "updated-pw"));
            e.CreateBackup(null);
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "final-pw"));

            Assert.Equal(2U, e.History.UCount);

            // The first history entry should hold the original password.
            string origPw = e.History.GetAt(0)
                .Strings.Get(PwDefs.PasswordField).ReadString();
            Assert.Equal("original-pw", origPw);
        }

        // ── 6. MergeIn strategies ─────────────────────────────────────────────

        [Fact]
        public void MergeIn_OverwriteExisting_ReplacesEntryInTarget()
        {
            // Database A: entry E1 with password "pw-A"
            PwDatabase dbA = MakeDb();
            PwEntry e1A = MakeEntry("Shared", "alice", "pw-A");
            dbA.RootGroup.AddEntry(e1A, true);
            PwUuid sharedUuid = e1A.Uuid;

            // Database B: same UUID entry E1 with password "pw-B" (newer modification)
            PwDatabase dbB = MakeDb();
            PwEntry e1B = new PwEntry(false, false);
            // Copy UUID so both refer to the "same" logical entry
            e1B.SetUuid(sharedUuid, true);
            e1B.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Shared"));
            e1B.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "alice"));
            e1B.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "pw-B"));
            // Ensure e1B is "newer" so OverwriteExisting logic applies
            e1B.LastModificationTime = e1A.LastModificationTime.AddSeconds(1);
            dbB.RootGroup.AddEntry(e1B, true);

            dbA.MergeIn(dbB, PwMergeMethod.OverwriteExisting);

            PwEntry merged = dbA.RootGroup.FindEntry(sharedUuid, true);
            Assert.NotNull(merged);
            Assert.Equal("pw-B", merged.Strings.Get(PwDefs.PasswordField).ReadString());
        }

        [Fact]
        public void MergeIn_KeepExisting_PreservesTargetEntry()
        {
            PwDatabase dbA = MakeDb();
            PwEntry e1A = MakeEntry("Shared", "alice", "pw-A");
            dbA.RootGroup.AddEntry(e1A, true);
            PwUuid sharedUuid = e1A.Uuid;

            PwDatabase dbB = MakeDb();
            PwEntry e1B = new PwEntry(false, false);
            e1B.SetUuid(sharedUuid, true);
            e1B.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Shared"));
            e1B.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "pw-B"));
            e1B.LastModificationTime = e1A.LastModificationTime.AddSeconds(1);
            dbB.RootGroup.AddEntry(e1B, true);

            dbA.MergeIn(dbB, PwMergeMethod.KeepExisting);

            PwEntry merged = dbA.RootGroup.FindEntry(sharedUuid, true);
            Assert.NotNull(merged);
            // KeepExisting: target's original value is preserved
            Assert.Equal("pw-A", merged.Strings.Get(PwDefs.PasswordField).ReadString());
        }

        [Fact]
        public void MergeIn_AddsUniqueEntriesFromSource()
        {
            PwDatabase dbA = MakeDb();
            dbA.RootGroup.AddEntry(MakeEntry("Entry-A-Only", "u", "p"), true);

            PwDatabase dbB = MakeDb();
            dbB.RootGroup.AddEntry(MakeEntry("Entry-B-Only", "u", "p"), true);

            dbA.MergeIn(dbB, PwMergeMethod.OverwriteExisting);

            // After merge, A should contain both entries
            Assert.Equal(2U, dbA.RootGroup.Entries.UCount);
        }

        // ── 7. Deleted object tracking ────────────────────────────────────────

        [Fact]
        public void DeletedObjects_AddTombstone_IsTracked()
        {
            PwDatabase db = MakeDb();
            PwUuid deletedUuid = new PwUuid(true);
            DateTime when = DateTime.UtcNow;

            db.DeletedObjects.Add(new PwDeletedObject(deletedUuid, when));

            Assert.Equal(1U, db.DeletedObjects.UCount);
            Assert.Equal(deletedUuid, db.DeletedObjects.GetAt(0).Uuid);
        }

        [Fact]
        public void DeletedObjects_MultipleEntries_CountIsCorrect()
        {
            PwDatabase db = MakeDb();
            for (int i = 0; i < 5; ++i)
                db.DeletedObjects.Add(new PwDeletedObject(new PwUuid(true), DateTime.UtcNow));

            Assert.Equal(5U, db.DeletedObjects.UCount);
        }

        // ── 8. Custom icon management ─────────────────────────────────────────

        [Fact]
        public void CustomIcons_AddIcon_IsFoundByUuid()
        {
            PwDatabase db = MakeDb();
            PwUuid iconUuid = new PwUuid(true);
            PwCustomIcon icon = new PwCustomIcon(iconUuid, s_minimalPng);
            db.CustomIcons.Add(icon);

            Assert.Equal(1, db.CustomIcons.Count);
            // Find by UUID via GetCustomIconIndex (GetCustomIcon returns Image, not PwCustomIcon)
            int idx = db.GetCustomIconIndex(iconUuid);
            Assert.True(idx >= 0, "Custom icon not found by UUID");
            Assert.Equal(iconUuid, db.CustomIcons[idx].Uuid);
        }

        [Fact]
        public void CustomIcons_Remove_CountDecrements()
        {
            PwDatabase db = MakeDb();
            PwUuid u1 = new PwUuid(true);
            PwUuid u2 = new PwUuid(true);
            db.CustomIcons.Add(new PwCustomIcon(u1, s_minimalPng));
            db.CustomIcons.Add(new PwCustomIcon(u2, s_minimalPng));

            Assert.Equal(2, db.CustomIcons.Count);
            db.CustomIcons.RemoveAll(ic => ic.Uuid.Equals(u1));
            Assert.Equal(1, db.CustomIcons.Count);
            Assert.Equal(-1, db.GetCustomIconIndex(u1));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static PwDatabase MakeDb()
        {
            PwDatabase db = new PwDatabase();
            db.RootGroup = new PwGroup(true, true, "Root", PwIcon.Folder);
            return db;
        }

        private static PwEntry MakeEntry(string title, string user, string password)
        {
            PwEntry e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, title));
            e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, user));
            e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  password));
            return e;
        }

        // Minimal 1×1 blue PNG — avoids System.Drawing dependency
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
