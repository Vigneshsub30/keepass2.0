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
using System.Linq;
using System.Reflection;

using KeePassLib;
using KeePassLib.Security;
using KeePass.Core.Projections;

using Xunit;

namespace KeePass.Tests.Platform
{
	public class EntryProjectionMapperTests
	{
		private readonly EntryProjectionMapper _mapper = new EntryProjectionMapper();

		// ── Null guard ────────────────────────────────────────────────────────

		[Fact]
		public void FromDomain_NullSource_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _mapper.FromDomain(null));
		}

		// ── Minimal entry ─────────────────────────────────────────────────────

		[Fact]
		public void FromDomain_MinimalEntry_UuidPreserved()
		{
			PwEntry e = ProjectionFixtures.MinimalEntry();
			EntryProjection p = _mapper.FromDomain(e);
			Assert.Equal(e.Uuid, p.Uuid);
		}

		[Fact]
		public void FromDomain_MinimalEntry_TitleMapped()
		{
			PwEntry e = ProjectionFixtures.MinimalEntry();
			EntryProjection p = _mapper.FromDomain(e);
			Assert.Equal("Minimal", p.Title.ReadString());
		}

		[Fact]
		public void FromDomain_MinimalEntry_EmptyCollections()
		{
			PwEntry e = ProjectionFixtures.MinimalEntry();
			EntryProjection p = _mapper.FromDomain(e);
			Assert.Empty(p.Tags);
			Assert.Empty(p.History);
			Assert.Empty(p.Binaries);
			Assert.Empty(p.CustomDataKeys);
		}

		[Fact]
		public void FromDomain_MinimalEntry_NoCustomFields()
		{
			PwEntry e = ProjectionFixtures.MinimalEntry();
			EntryProjection p = _mapper.FromDomain(e);
			// Standard fields not in custom fields dict
			Assert.DoesNotContain(PwDefs.TitleField,    (IDictionary<string, ProtectedString>)p.CustomFields);
			Assert.DoesNotContain(PwDefs.PasswordField, (IDictionary<string, ProtectedString>)p.CustomFields);
		}

		// ── Full entry ────────────────────────────────────────────────────────

		[Fact]
		public void FromDomain_FullEntry_StandardFieldsMapped()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.Equal("Full Entry",             p.Title.ReadString());
			Assert.Equal("alice",                  p.UserName.ReadString());
			Assert.Equal("s3cret",                 p.Password.ReadString());
			Assert.Equal("https://example.com",    p.Url.ReadString());
			Assert.Equal("Some notes.",             p.Notes.ReadString());
		}

		[Fact]
		public void FromDomain_FullEntry_PasswordIsProtected()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);
			Assert.True(p.Password.IsProtected);
		}

		[Fact]
		public void FromDomain_FullEntry_CustomFieldsMapped()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.True(p.CustomFields.ContainsKey("CustomField1"));
			Assert.True(p.CustomFields.ContainsKey("CustomField2"));
			Assert.Equal("cv1", p.CustomFields["CustomField1"].ReadString());
			Assert.Equal("cv2-protected", p.CustomFields["CustomField2"].ReadString());
		}

		[Fact]
		public void FromDomain_FullEntry_TagsMapped()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.Equal(2, p.Tags.Count);
			Assert.Contains("finance", p.Tags);
			Assert.Contains("personal", p.Tags);
		}

		[Fact]
		public void FromDomain_FullEntry_TimestampsMapped()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.Equal(e.CreationTime,         p.CreationTime);
			Assert.Equal(e.LastModificationTime, p.LastModificationTime);
			Assert.Equal(e.LastAccessTime,       p.LastAccessTime);
			Assert.True(p.Expires);
			Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), p.ExpiryTime);
		}

		[Fact]
		public void FromDomain_FullEntry_UsageMapped()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.Equal(5UL, p.UsageCount);
			Assert.True(p.QualityCheck);
		}

		[Fact]
		public void FromDomain_FullEntry_AutoTypeMapped()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.True(p.AutoTypeEnabled);
			Assert.Equal("{USERNAME}{TAB}{PASSWORD}{ENTER}", p.AutoTypeSequence);
		}

		[Fact]
		public void FromDomain_FullEntry_OverrideUrlMapped()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);
			Assert.Equal("https://override.example.com", p.OverrideUrl);
		}

		[Fact]
		public void FromDomain_FullEntry_HistorySummaryGenerated()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.Single(p.History);
			Assert.Equal("Old Title", p.History[0].Title);
		}

		[Fact]
		public void FromDomain_FullEntry_BinaryReferencesGenerated()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			Assert.Single(p.Binaries);
			BinaryReference binRef = p.Binaries[0];
			Assert.Equal("doc.pdf", binRef.Name);
			Assert.Equal(3L, binRef.Size);
			Assert.NotEmpty(binRef.ContentHash);
		}

		[Fact]
		public void FromDomain_FullEntry_BinaryHashIsHex()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);
			// SHA-256 is 64 hex chars
			Assert.Equal(64, p.Binaries[0].ContentHash.Length);
		}

		// ── Color handling ────────────────────────────────────────────────────

		[Fact]
		public void FromDomain_AnyEntry_ColorHexIsNullInCrossPlatformBuild()
		{
			// In net10.0 (KeePassUAP) the Color properties do not exist on PwEntry;
			// the mapper explicitly sets them to null.
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);
			Assert.Null(p.ForegroundColorHex);
			Assert.Null(p.BackgroundColorHex);
		}

		// ── Immutability guarantee ────────────────────────────────────────────

		[Fact]
		public void FromDomain_MutatingSourceAfterProjection_DoesNotAffectProjection()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);

			string originalTitle = p.Title.ReadString();
			// Mutate the source
			e.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "MUTATED"));

			// Projection must be unaffected
			Assert.Equal(originalTitle, p.Title.ReadString());
		}

		[Fact]
		public void FromDomain_MutatingSourceTags_DoesNotAffectProjection()
		{
			PwEntry e = ProjectionFixtures.FullEntry();
			EntryProjection p = _mapper.FromDomain(e);
			int originalCount = p.Tags.Count;

			e.Tags.Add("injected");

			Assert.Equal(originalCount, p.Tags.Count);
		}

		// ── Reflection-based completeness ────────────────────────────────────

		[Fact]
		public void EntryProjection_CoversAllPwEntryStandardFields()
		{
			// Verify that each standard string field has a corresponding
			// named property on EntryProjection.
			var projectionProps = new HashSet<string>(
				typeof(EntryProjection).GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => p.Name),
				StringComparer.OrdinalIgnoreCase);

			// These are the standard PwEntry string field names mapped to projection properties.
			var expectedMappings = new Dictionary<string, string>
			{
				{ PwDefs.TitleField,    "Title"    },
				{ PwDefs.UserNameField, "UserName" },
				{ PwDefs.PasswordField, "Password" },
				{ PwDefs.UrlField,      "Url"      },
				{ PwDefs.NotesField,    "Notes"    },
			};

			foreach(var kvp in expectedMappings)
				Assert.True(projectionProps.Contains(kvp.Value),
					$"EntryProjection is missing property '{kvp.Value}' for PwEntry field '{kvp.Key}'.");
		}

		[Fact]
		public void EntryProjection_StructuralProperties_AllPresent()
		{
			// Enumerate known PwEntry structural properties and verify coverage.
			var required = new[]
			{
				"Uuid", "ParentGroupUuid", "IconId", "CustomIconUuid",
				"ForegroundColorHex", "BackgroundColorHex",
				"Tags", "CreationTime", "LastModificationTime", "LastAccessTime",
				"ExpiryTime", "Expires", "UsageCount", "QualityCheck",
				"AutoTypeEnabled", "AutoTypeSequence", "OverrideUrl",
				"CustomFields", "CustomDataKeys", "History", "Binaries",
			};

			var projProps = new HashSet<string>(
				typeof(EntryProjection).GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => p.Name));

			foreach(string prop in required)
				Assert.True(projProps.Contains(prop),
					$"EntryProjection is missing expected property '{prop}'.");
		}
	}
}
