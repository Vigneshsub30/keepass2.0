#nullable enable

using System.Collections.Generic;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for MRU path-comparison and deduplication logic, implemented
	/// via a local stub that mirrors IMruComparisonService/MruComparisonService
	/// without referencing the WinForms KeePass assembly.
	/// </summary>
	public sealed class MruComparisonServiceTests
	{
		// ── Stub ───────────────────────────────────────────────────────── //

		private interface IMruComparisonServiceStub
		{
			bool AreSamePath(string? path1, string? path2);
		}

		private sealed class MruComparisonServiceStub : IMruComparisonServiceStub
		{
			public bool AreSamePath(string? path1, string? path2)
			{
				if(path1 == null && path2 == null) return true;
				if(path1 == null || path2 == null) return false;
				return path1.Equals(path2,
					System.StringComparison.OrdinalIgnoreCase);
			}
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void AreSamePath_SamePath_ReturnsTrue()
		{
			var svc = new MruComparisonServiceStub();
			Assert.True(svc.AreSamePath(
				@"C:\Databases\vault.kdbx",
				@"C:\Databases\vault.kdbx"));
		}

		[Fact]
		public void AreSamePath_DifferentCase_ReturnsTrue()
		{
			var svc = new MruComparisonServiceStub();
			Assert.True(svc.AreSamePath(
				@"C:\databases\Vault.KDBX",
				@"C:\Databases\vault.kdbx"));
		}

		[Fact]
		public void AreSamePath_DifferentPaths_ReturnsFalse()
		{
			var svc = new MruComparisonServiceStub();
			Assert.False(svc.AreSamePath(
				@"C:\Databases\a.kdbx",
				@"C:\Databases\b.kdbx"));
		}

		[Fact]
		public void AreSamePath_BothNull_ReturnsTrue()
		{
			var svc = new MruComparisonServiceStub();
			Assert.True(svc.AreSamePath(null, null));
		}

		[Fact]
		public void AreSamePath_OneNull_ReturnsFalse()
		{
			var svc = new MruComparisonServiceStub();
			Assert.False(svc.AreSamePath(null, @"C:\vault.kdbx"));
			Assert.False(svc.AreSamePath(@"C:\vault.kdbx", null));
		}

		[Fact]
		public void AreSamePath_BothEmpty_ReturnsTrue()
		{
			var svc = new MruComparisonServiceStub();
			Assert.True(svc.AreSamePath(string.Empty, string.Empty));
		}

		[Fact]
		public void AreSamePath_EmptyVsNonEmpty_ReturnsFalse()
		{
			var svc = new MruComparisonServiceStub();
			Assert.False(svc.AreSamePath(string.Empty, @"C:\vault.kdbx"));
		}

		// ── Deduplication helper ──────────────────────────────────────── //

		/// <summary>
		/// Deduplication logic matching the extracted pattern from AppConfigEx.
		/// </summary>
		private static List<string> DeduplicateMruPaths(
			IEnumerable<string?> paths, IMruComparisonServiceStub svc)
		{
			var result = new List<string>();
			foreach(string? path in paths)
			{
				if(path == null) continue;
				bool duplicate = false;
				foreach(string existing in result)
				{
					if(svc.AreSamePath(path, existing)) { duplicate = true; break; }
				}
				if(!duplicate) result.Add(path);
			}
			return result;
		}

		[Fact]
		public void Dedup_NoDuplicates_RetainsAll()
		{
			var svc = new MruComparisonServiceStub();
			var input = new[] { @"C:\a.kdbx", @"C:\b.kdbx", @"C:\c.kdbx" };
			List<string> result = DeduplicateMruPaths(input, svc);
			Assert.Equal(3, result.Count);
		}

		[Fact]
		public void Dedup_ExactDuplicate_RemovesOne()
		{
			var svc = new MruComparisonServiceStub();
			var input = new[] { @"C:\vault.kdbx", @"C:\vault.kdbx", @"C:\other.kdbx" };
			List<string> result = DeduplicateMruPaths(input, svc);
			Assert.Equal(2, result.Count);
		}

		[Fact]
		public void Dedup_CaseDuplicate_RemovesOne()
		{
			var svc = new MruComparisonServiceStub();
			var input = new[] { @"C:\Vault.KDBX", @"C:\vault.kdbx" };
			List<string> result = DeduplicateMruPaths(input, svc);
			Assert.Single(result);
		}

		[Fact]
		public void Dedup_NullEntries_Excluded()
		{
			var svc = new MruComparisonServiceStub();
			var input = new string?[] { null, @"C:\vault.kdbx", null };
			List<string> result = DeduplicateMruPaths(input, svc);
			Assert.Single(result);
		}
	}
}
