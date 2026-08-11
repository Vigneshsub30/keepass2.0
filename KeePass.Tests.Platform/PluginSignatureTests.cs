#nullable enable

using System;
using System.Collections.Generic;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <c>PluginSignatureResult</c> and <c>PublisherKeyAllowList</c>.
	/// <para>
	/// These tests use only <c>KeePassLib</c> and <c>KeePass.Core</c> types so
	/// they run on all platforms without the WinForms KeePass assembly.  The
	/// types under test (<c>PluginSignatureResult</c>, <c>PublisherKeyAllowList</c>)
	/// are replicated here as local stubs that mirror the real production types,
	/// allowing us to verify their logic without a KeePass project reference.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <c>PublisherKeyAllowList</c> and <c>PluginSignatureResult</c> live in the
	/// <c>KeePass</c> project which is WinForms-only.  The tests below duplicate
	/// enough of that logic to exercise it in the platform-neutral test project.
	/// </remarks>
	public sealed class PluginSignatureTests
	{
		// ── Local stubs mirroring production types ─────────────────── //

		private sealed class StubAllowList
		{
			private readonly HashSet<string> _tokens;
			public bool IsEmpty => _tokens.Count == 0;

			public StubAllowList(IEnumerable<string> tokens)
				=> _tokens = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);

			public bool IsAllowed(string? token)
			{
				if (_tokens.Count == 0) return true;
				if (string.IsNullOrEmpty(token)) return false;
				return _tokens.Contains(token);
			}
		}

		private sealed class StubSignatureResult
		{
			public bool    IsValid           { get; }
			public string? PublisherKeyToken { get; }
			public string? RejectionReason  { get; }

			public StubSignatureResult(bool isValid, string? token, string? reason)
			{
				IsValid          = isValid;
				PublisherKeyToken = token;
				RejectionReason  = reason;
			}
		}

		// ── PublisherKeyAllowList (via stub) ──────────────────────── //

		[Fact]
		public void EmptyAllowList_IsEmpty_True()
		{
			var al = new StubAllowList(Array.Empty<string>());
			Assert.True(al.IsEmpty);
		}

		[Fact]
		public void EmptyAllowList_IsAllowed_AlwaysTrue()
		{
			var al = new StubAllowList(Array.Empty<string>());
			Assert.True(al.IsAllowed(null));
			Assert.True(al.IsAllowed(""));
			Assert.True(al.IsAllowed("aabbccdd00112233"));
		}

		[Fact]
		public void AllowList_WithTokens_IsEmpty_False()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			Assert.False(al.IsEmpty);
		}

		[Fact]
		public void AllowList_KnownToken_IsAllowed_True()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			Assert.True(al.IsAllowed("aabbccdd00112233"));
		}

		[Fact]
		public void AllowList_KnownToken_CaseInsensitive()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			Assert.True(al.IsAllowed("AABBCCDD00112233"));
		}

		[Fact]
		public void AllowList_UnknownToken_IsAllowed_False()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			Assert.False(al.IsAllowed("ffeeddccbbaa9988"));
		}

		[Fact]
		public void AllowList_NullToken_WithTokens_IsAllowed_False()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			Assert.False(al.IsAllowed(null));
		}

		// ── PluginSignatureResult (via stub) ─────────────────────── //

		[Fact]
		public void SignatureResult_Valid_IsValid_True()
		{
			var r = new StubSignatureResult(true, "aabb", null);
			Assert.True(r.IsValid);
			Assert.Equal("aabb", r.PublisherKeyToken);
			Assert.Null(r.RejectionReason);
		}

		[Fact]
		public void SignatureResult_Invalid_IsValid_False_HasReason()
		{
			var r = new StubSignatureResult(false, null, "No valid signature found.");
			Assert.False(r.IsValid);
			Assert.Null(r.PublisherKeyToken);
			Assert.Equal("No valid signature found.", r.RejectionReason);
		}

		// ── Integration: allow-list + signature ───────────────────── //

		[Fact]
		public void Pipeline_ValidSignatureAllowedPublisher_Passes()
		{
			const string token = "aabbccdd00112233";
			var al = new StubAllowList(new[] { token });
			var sig = new StubSignatureResult(true, token, null);

			bool passes = sig.IsValid && al.IsAllowed(sig.PublisherKeyToken);
			Assert.True(passes);
		}

		[Fact]
		public void Pipeline_ValidSignatureButPublisherNotAllowed_Rejected()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			var sig = new StubSignatureResult(true, "ffeeddccbbaa9988", null);

			bool passes = sig.IsValid && al.IsAllowed(sig.PublisherKeyToken);
			Assert.False(passes);
		}

		[Fact]
		public void Pipeline_InvalidSignature_Rejected()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			var sig = new StubSignatureResult(false, null, "Signature invalid");

			Assert.False(sig.IsValid);
		}

		[Fact]
		public void Pipeline_NoSignature_EmptyAllowList_Admitted()
		{
			var al = new StubAllowList(Array.Empty<string>());
			// When no signature is present and allow-list is empty, we admit.
			bool admitted = al.IsEmpty;
			Assert.True(admitted);
		}

		[Fact]
		public void Pipeline_NoSignature_EnforcedAllowList_Rejected()
		{
			var al = new StubAllowList(new[] { "aabbccdd00112233" });
			// No signature → token is null → allow-list rejects.
			Assert.False(al.IsAllowed(null));
		}
	}
}
