/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Collections.Generic;

using KeePassLib;
using KeePassLib.Security;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Factory methods for <see cref="PwEntry"/> and <see cref="PwGroup"/>
	/// test fixtures used by WO-045 projection mapper tests.
	/// </summary>
	public static class ProjectionFixtures
	{
		// ── PwEntry fixtures ─────────────────────────────────────────────────

		/// <summary>
		/// Minimal entry: only UUID, title, and username populated.
		/// All collections are empty.
		/// </summary>
		public static PwEntry MinimalEntry()
		{
			var e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Minimal"));
			e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "user@example.com"));
			return e;
		}

		/// <summary>
		/// Full entry with all standard fields, two custom fields, two tags,
		/// one history snapshot, and one binary attachment.
		/// </summary>
		public static PwEntry FullEntry()
		{
			var e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Full Entry"));
			e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "alice"));
			e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "s3cret"));
			e.Strings.Set(PwDefs.UrlField,      new ProtectedString(false, "https://example.com"));
			e.Strings.Set(PwDefs.NotesField,    new ProtectedString(false, "Some notes."));
			e.Strings.Set("CustomField1",       new ProtectedString(false, "cv1"));
			e.Strings.Set("CustomField2",       new ProtectedString(true,  "cv2-protected"));

			e.Tags = new List<string> { "finance", "personal" };

			e.Expires  = true;
			e.ExpiryTime = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			e.UsageCount = 5;
			e.QualityCheck = true;
			e.AutoType.Enabled = true;
			e.AutoType.DefaultSequence = "{USERNAME}{TAB}{PASSWORD}{ENTER}";
			e.OverrideUrl = "https://override.example.com";

			// Binary attachment
			byte[] content = new byte[] { 0x50, 0x44, 0x46 }; // "PDF"
			e.Binaries.Set("doc.pdf", new ProtectedBinary(false, content));

			// History snapshot
			var hist = new PwEntry(false, false);
			hist.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Old Title"));
			e.History.Add(hist);

			return e;
		}

		/// <summary>
		/// Entry with custom fields and tags, but no history or binaries.
		/// </summary>
		public static PwEntry CustomFieldsEntry()
		{
			var e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,  new ProtectedString(false, "Custom Fields Entry"));
			e.Strings.Set("TOTP",             new ProtectedString(true,  "otpseed"));
			e.Strings.Set("AccountNumber",    new ProtectedString(false, "123-456"));
			e.Tags = new List<string> { "otp", "bank" };
			return e;
		}

		// ── PwGroup fixtures ─────────────────────────────────────────────────

		/// <summary>
		/// Root group (no parent) with two child groups and one child entry.
		/// </summary>
		public static PwGroup RootGroup()
		{
			var root = new PwGroup(true, true, "Root", PwIcon.Folder);

			var child = new PwGroup(true, true, "Child", PwIcon.Folder);
			root.AddGroup(child, true);

			var entry = new PwEntry(true, true);
			entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Entry"));
			root.AddEntry(entry, true);

			return root;
		}

		/// <summary>
		/// Nested group: Parent → Child → Grandchild.
		/// Returns the grandchild (deepest) group.
		/// </summary>
		public static PwGroup NestedGroup()
		{
			var parent = new PwGroup(true, true, "Parent", PwIcon.Folder);
			var child  = new PwGroup(true, true, "Child",  PwIcon.Folder);
			var grand  = new PwGroup(true, true, "Grand",  PwIcon.Folder);
			parent.AddGroup(child, true);
			child.AddGroup(grand, true);
			return grand;
		}
	}
}
