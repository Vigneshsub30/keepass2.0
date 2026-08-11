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
using KeePass.Core.Projections;

using Xunit;

namespace KeePass.Tests.Platform
{
	public class GroupProjectionMapperTests
	{
		private readonly GroupProjectionMapper _mapper = new GroupProjectionMapper();

		// ── Null guard ────────────────────────────────────────────────────────

		[Fact]
		public void FromDomain_NullSource_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _mapper.FromDomain(null));
		}

		// ── Root group ────────────────────────────────────────────────────────

		[Fact]
		public void FromDomain_RootGroup_UuidPreserved()
		{
			PwGroup root = ProjectionFixtures.RootGroup();
			GroupProjection p = _mapper.FromDomain(root);
			Assert.Equal(root.Uuid, p.Uuid);
		}

		[Fact]
		public void FromDomain_RootGroup_NameMapped()
		{
			PwGroup root = ProjectionFixtures.RootGroup();
			GroupProjection p = _mapper.FromDomain(root);
			Assert.Equal("Root", p.Name);
		}

		[Fact]
		public void FromDomain_RootGroup_ParentIsZero()
		{
			PwGroup root = ProjectionFixtures.RootGroup();
			GroupProjection p = _mapper.FromDomain(root);
			Assert.Equal(PwUuid.Zero, p.ParentGroupUuid);
		}

		[Fact]
		public void FromDomain_RootGroup_DepthIsZero()
		{
			PwGroup root = ProjectionFixtures.RootGroup();
			GroupProjection p = _mapper.FromDomain(root);
			Assert.Equal(0, p.Depth);
		}

		[Fact]
		public void FromDomain_RootGroup_ChildCountsCorrect()
		{
			PwGroup root = ProjectionFixtures.RootGroup();
			GroupProjection p = _mapper.FromDomain(root);
			Assert.Equal(1, p.ChildGroupCount);   // one child group
			Assert.Equal(1, p.ChildEntryCount);   // one child entry
		}

		[Fact]
		public void FromDomain_RootGroup_FullPathContainsName()
		{
			PwGroup root = ProjectionFixtures.RootGroup();
			GroupProjection p = _mapper.FromDomain(root);
			Assert.Contains("Root", p.FullPath);
		}

		// ── Nested group ──────────────────────────────────────────────────────

		[Fact]
		public void FromDomain_NestedGroup_DepthIsTwo()
		{
			PwGroup grand = ProjectionFixtures.NestedGroup();
			GroupProjection p = _mapper.FromDomain(grand);
			// Parent → Child → Grand: depth = 2
			Assert.Equal(2, p.Depth);
		}

		[Fact]
		public void FromDomain_NestedGroup_ParentGroupUuidSet()
		{
			PwGroup grand = ProjectionFixtures.NestedGroup();
			GroupProjection p = _mapper.FromDomain(grand);
			Assert.NotEqual(PwUuid.Zero, p.ParentGroupUuid);
		}

		[Fact]
		public void FromDomain_NestedGroup_FullPathContainsAllNames()
		{
			PwGroup grand = ProjectionFixtures.NestedGroup();
			GroupProjection p = _mapper.FromDomain(grand);
			Assert.Contains("Parent", p.FullPath);
			Assert.Contains("Child",  p.FullPath);
			Assert.Contains("Grand",  p.FullPath);
		}

		[Fact]
		public void FromDomain_NestedGroup_NoChildren()
		{
			PwGroup grand = ProjectionFixtures.NestedGroup();
			GroupProjection p = _mapper.FromDomain(grand);
			Assert.Equal(0, p.ChildGroupCount);
			Assert.Equal(0, p.ChildEntryCount);
		}

		// ── Timestamps & flags ────────────────────────────────────────────────

		[Fact]
		public void FromDomain_Group_TimestampsMapped()
		{
			PwGroup g = new PwGroup(true, true, "G", PwIcon.Folder);
			GroupProjection p = _mapper.FromDomain(g);
			Assert.Equal(g.CreationTime,         p.CreationTime);
			Assert.Equal(g.LastModificationTime, p.LastModificationTime);
		}

		[Fact]
		public void FromDomain_Group_ExpiresMapped()
		{
			PwGroup g = new PwGroup(true, true, "G", PwIcon.Folder);
			g.Expires    = true;
			g.ExpiryTime = new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
			GroupProjection p = _mapper.FromDomain(g);
			Assert.True(p.Expires);
			Assert.Equal(new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc), p.ExpiryTime);
		}

		[Fact]
		public void FromDomain_Group_EnableAutoTypeMapped()
		{
			PwGroup g = new PwGroup(true, true, "G", PwIcon.Folder);
			g.EnableAutoType = false;
			GroupProjection p = _mapper.FromDomain(g);
			Assert.Equal(false, p.EnableAutoType);
		}

		[Fact]
		public void FromDomain_Group_EnableSearchingNullInherited()
		{
			PwGroup g = new PwGroup(true, true, "G", PwIcon.Folder);
			// Default is null (inherit from parent)
			GroupProjection p = _mapper.FromDomain(g);
			Assert.Null(p.EnableSearching);
		}

		[Fact]
		public void FromDomain_Group_DefaultAutoTypeSequenceMapped()
		{
			PwGroup g = new PwGroup(true, true, "G", PwIcon.Folder);
			g.DefaultAutoTypeSequence = "{USERNAME}{TAB}{PASSWORD}";
			GroupProjection p = _mapper.FromDomain(g);
			Assert.Equal("{USERNAME}{TAB}{PASSWORD}", p.DefaultAutoTypeSequence);
		}

		// ── Immutability guarantee ────────────────────────────────────────────

		[Fact]
		public void FromDomain_MutatingSourceAfterProjection_DoesNotAffectProjection()
		{
			PwGroup g = new PwGroup(true, true, "Original", PwIcon.Folder);
			GroupProjection p = _mapper.FromDomain(g);

			g.Name = "MUTATED";

			Assert.Equal("Original", p.Name);
		}

		[Fact]
		public void FromDomain_MutatingSourceTags_DoesNotAffectProjection()
		{
			PwGroup g = new PwGroup(true, true, "G", PwIcon.Folder);
			g.Tags = new List<string> { "tag1" };
			GroupProjection p = _mapper.FromDomain(g);
			int count = p.Tags.Count;

			g.Tags.Add("injected");

			Assert.Equal(count, p.Tags.Count);
		}

		// ── Reflection-based completeness ─────────────────────────────────────

		[Fact]
		public void GroupProjection_StructuralProperties_AllPresent()
		{
			var required = new[]
			{
				"Uuid", "ParentGroupUuid", "Name", "Notes", "IconId", "CustomIconUuid",
				"IsExpanded", "Tags", "EnableAutoType", "EnableSearching",
				"DefaultAutoTypeSequence", "CreationTime", "LastModificationTime",
				"ExpiryTime", "Expires", "CustomDataKeys",
				"FullPath", "Depth", "ChildGroupCount", "ChildEntryCount",
			};

			var projProps = new HashSet<string>(
				typeof(GroupProjection).GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => p.Name));

			foreach(string prop in required)
				Assert.True(projProps.Contains(prop),
					$"GroupProjection is missing expected property '{prop}'.");
		}

		[Fact]
		public void GroupProjection_PwGroupPublicProperties_AllMapped()
		{
			// Enumerate PwGroup public instance properties and verify each has
			// a corresponding property on GroupProjection (by name or known alias).
			var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				// Direct name matches
				"Uuid", "Name", "Notes", "IconId", "CustomIconUuid",
				"IsExpanded", "IsVirtual", "Expires", "ExpiryTime",
				"CreationTime", "LastModificationTime", "LastAccessTime",
				"UsageCount", "LocationChanged", "DefaultAutoTypeSequence",
				"EnableAutoType", "EnableSearching", "Tags",
				// Aliased or computed on projection
				"ParentGroup",            // → ParentGroupUuid
				"PreviousParentGroup",    // intentionally not projected (history detail)
				"Groups",                 // → ChildGroupCount
				"Entries",               // → ChildEntryCount
				"LastTopVisibleEntry",   // intentionally not projected (UI state)
				"CustomData",            // → CustomDataKeys
			};

			var pwGroupProps = typeof(PwGroup)
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Select(p => p.Name)
				.ToList();

			// Every PwGroup property should be accounted for in 'known'
			foreach(string prop in pwGroupProps)
				Assert.True(known.Contains(prop),
					$"PwGroup property '{prop}' is not accounted for in GroupProjection mapping.");
		}
	}
}
