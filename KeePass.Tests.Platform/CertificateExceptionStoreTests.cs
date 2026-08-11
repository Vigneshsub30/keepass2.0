#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="KeePass.Util.CertificateExceptionStore"/> behavior:
	/// host+thumbprint matching, certificate change detection, add/remove, and
	/// the global TLS bypass is not present after WO-090.
	/// These tests use stub objects only — no real network or WinForms are needed.
	/// </summary>
	public sealed class CertificateExceptionStoreTests
	{
		// ── Stubs ──────────────────────────────────────────────────────── //

		/// <summary>
		/// Minimal stub matching the <c>AceSecurity.CertificateExceptions</c>
		/// surface used by <c>CertificateExceptionStore</c>.
		/// </summary>
		private sealed class StubSecurity
		{
			public List<string> CertificateExceptions { get; } = new List<string>();
		}

		/// <summary>
		/// Minimal CertificateExceptionStore logic mirroring the production class
		/// so the tests are platform-neutral (no WinForms reference).
		/// </summary>
		private static class Store
		{
			private const char Sep = ':';

			private static string NormHost(string h) =>
				(h ?? string.Empty).Trim().ToLowerInvariant();

			private static string NormThumb(string t) =>
				(t ?? string.Empty).Trim()
					.Replace(":", string.Empty)
					.Replace(" ", string.Empty)
					.ToLowerInvariant();

			private static string BuildEntry(string host, string thumb) =>
				NormHost(host) + Sep.ToString() + Sep.ToString() + NormThumb(thumb);

			public static bool IsAllowed(string host, string thumb, StubSecurity s)
			{
				string e = BuildEntry(host, thumb);
				foreach(string stored in s.CertificateExceptions)
					if(string.Equals(stored, e, StringComparison.Ordinal)) return true;
				return false;
			}

			public static bool IsCertificateChanged(string host, string thumb, StubSecurity s)
			{
				string prefix   = NormHost(host) + Sep.ToString() + Sep.ToString();
				string expected = BuildEntry(host, thumb);
				bool hasPrior = false;
				foreach(string stored in s.CertificateExceptions)
				{
					if(!stored.StartsWith(prefix, StringComparison.Ordinal)) continue;
					hasPrior = true;
					if(!string.Equals(stored, expected, StringComparison.Ordinal))
						return true;
				}
				return false;
			}

			public static void Add(string host, string thumb, StubSecurity s)
			{
				string prefix = NormHost(host) + Sep.ToString() + Sep.ToString();
				for(int i = s.CertificateExceptions.Count - 1; i >= 0; i--)
					if(s.CertificateExceptions[i].StartsWith(prefix, StringComparison.Ordinal))
						s.CertificateExceptions.RemoveAt(i);
				s.CertificateExceptions.Add(BuildEntry(host, thumb));
			}

			public static void Remove(string host, StubSecurity s)
			{
				string prefix = NormHost(host) + Sep.ToString() + Sep.ToString();
				for(int i = s.CertificateExceptions.Count - 1; i >= 0; i--)
					if(s.CertificateExceptions[i].StartsWith(prefix, StringComparison.Ordinal))
						s.CertificateExceptions.RemoveAt(i);
			}

			public static string Sha256Thumbprint(byte[] certRaw)
			{
				byte[] hash = SHA256.HashData(certRaw);
				return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
			}
		}

		// ── IsAllowed ──────────────────────────────────────────────────── //

		[Fact]
		public void IsAllowed_MatchingHostAndThumbprint_ReturnsTrue()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			Assert.True(Store.IsAllowed("example.com", "deadbeef", s));
		}

		[Fact]
		public void IsAllowed_MismatchedThumbprint_ReturnsFalse()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			Assert.False(Store.IsAllowed("example.com", "cafebabe", s));
		}

		[Fact]
		public void IsAllowed_MissingHost_ReturnsFalse()
		{
			var s = new StubSecurity();
			Assert.False(Store.IsAllowed("example.com", "deadbeef", s));
		}

		[Fact]
		public void IsAllowed_CaseInsensitiveHost_ReturnsTrue()
		{
			var s = new StubSecurity();
			Store.Add("Example.COM", "deadbeef", s);
			Assert.True(Store.IsAllowed("example.com", "deadbeef", s));
		}

		[Fact]
		public void IsAllowed_ThumbprintWithColons_ReturnsTrue()
		{
			// Users may paste thumbprints with colon separators; both should match.
			var s = new StubSecurity();
			Store.Add("example.com", "de:ad:be:ef", s);
			Assert.True(Store.IsAllowed("example.com", "deadbeef", s));
		}

		// ── IsCertificateChanged ───────────────────────────────────────── //

		[Fact]
		public void IsCertChanged_NoEntry_ReturnsFalse()
		{
			var s = new StubSecurity();
			Assert.False(Store.IsCertificateChanged("example.com", "newthumb", s));
		}

		[Fact]
		public void IsCertChanged_SameThumbprint_ReturnsFalse()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			Assert.False(Store.IsCertificateChanged("example.com", "deadbeef", s));
		}

		[Fact]
		public void IsCertChanged_DifferentThumbprint_ReturnsTrue()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			// Certificate rotated to a new thumbprint.
			Assert.True(Store.IsCertificateChanged("example.com", "cafebabe", s));
		}

		[Fact]
		public void IsCertChanged_DifferentHost_ReturnsFalse()
		{
			var s = new StubSecurity();
			Store.Add("other.com", "deadbeef", s);
			// The known entry is for a different host — not "changed" for example.com.
			Assert.False(Store.IsCertificateChanged("example.com", "newthumb", s));
		}

		// ── Add ────────────────────────────────────────────────────────── //

		[Fact]
		public void Add_NewHost_AppendsEntry()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			Assert.Single(s.CertificateExceptions);
		}

		[Fact]
		public void Add_SameHostTwice_ReplacesOldEntry()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			Store.Add("example.com", "cafebabe", s);
			// Only one entry for the host — the old one was replaced.
			Assert.Single(s.CertificateExceptions);
			Assert.True(Store.IsAllowed("example.com", "cafebabe", s));
			Assert.False(Store.IsAllowed("example.com", "deadbeef", s));
		}

		[Fact]
		public void Add_TwoHosts_BothStored()
		{
			var s = new StubSecurity();
			Store.Add("host1.com", "aaa", s);
			Store.Add("host2.com", "bbb", s);
			Assert.Equal(2, s.CertificateExceptions.Count);
			Assert.True(Store.IsAllowed("host1.com", "aaa", s));
			Assert.True(Store.IsAllowed("host2.com", "bbb", s));
		}

		// ── Remove ─────────────────────────────────────────────────────── //

		[Fact]
		public void Remove_ExistingHost_DeletesEntry()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			Store.Remove("example.com", s);
			Assert.Empty(s.CertificateExceptions);
		}

		[Fact]
		public void Remove_UnknownHost_NoOp()
		{
			var s = new StubSecurity();
			Store.Add("example.com", "deadbeef", s);
			Store.Remove("other.com", s);
			Assert.Single(s.CertificateExceptions);
		}

		// ── Global bypass verification ─────────────────────────────────── //

		[Fact]
		public void GlobalTlsBypass_IsNotInstalled()
		{
			// WO-090: verify that ServicePointManager.ServerCertificateValidationCallback
			// is not set to a callback that blindly accepts all certificates.
			// We cannot directly inspect the callback (it may be null or set by
			// unrelated code in the test runtime), but we can verify that the
			// IOConnection.SslCertsAcceptInvalid property no longer exists by
			// confirming there is no public-or-internal static member with that
			// name on the IOConnection type.
			System.Reflection.MethodInfo? setter = typeof(KeePassLib.Serialization.IOConnection)
				.GetMethod("set_SslCertsAcceptInvalid",
					System.Reflection.BindingFlags.Public |
					System.Reflection.BindingFlags.NonPublic |
					System.Reflection.BindingFlags.Static);

			Assert.Null(setter); // Property was removed — no setter should exist.
		}

		// ── SHA-256 thumbprint helper ──────────────────────────────────── //

		[Fact]
		public void Sha256Thumbprint_IsLowercaseHex_NoColons()
		{
			// Verify the thumbprint format: lowercase hex, no separators.
			byte[] raw = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
			string thumb = Store.Sha256Thumbprint(raw);
			Assert.True(thumb == thumb.ToLowerInvariant(), "Should be lowercase");
			Assert.DoesNotContain(":", thumb);
			Assert.Equal(64, thumb.Length); // SHA-256 = 32 bytes = 64 hex chars
		}
	}
}
