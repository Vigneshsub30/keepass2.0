/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;

using KeePass.Core.Platform;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Characterization tests for <see cref="ICredentialStore"/> implementations
	/// on each Unix platform (WO-044).
	///
	/// Tests that invoke real platform credential APIs (macOS Keychain via
	/// <c>security</c>; Linux Secret Service via <c>secret-tool</c>) are
	/// guarded by platform checks and return early when not on the target OS
	/// or when the CLI tool is not installed.
	/// </summary>
	public sealed class CredentialStoreTests
	{
		private static readonly string UniqueKey =
			TestFixtures.CredentialTestKey + "." + Guid.NewGuid();

		// ── Argument validation (all platforms) ───────────────────────────

		[Fact]
		public void MacKeychainStore_Store_NullKey_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new KeePass.Platform.Unix.Mac.MacKeychainStore()
					.Store(null, TestFixtures.CredentialTestSecret));
		}

		[Fact]
		public void MacKeychainStore_Store_EmptyKey_Throws()
		{
			Assert.Throws<ArgumentException>(() =>
				new KeePass.Platform.Unix.Mac.MacKeychainStore()
					.Store(string.Empty, TestFixtures.CredentialTestSecret));
		}

		[Fact]
		public void MacKeychainStore_Store_NullSecret_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new KeePass.Platform.Unix.Mac.MacKeychainStore().Store("key", null));
		}

		[Fact]
		public void MacKeychainStore_Store_EmptySecret_Throws()
		{
			Assert.Throws<ArgumentException>(() =>
				new KeePass.Platform.Unix.Mac.MacKeychainStore()
					.Store("key", new byte[0]));
		}

		[Fact]
		public void MacKeychainStore_Retrieve_NullKey_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new KeePass.Platform.Unix.Mac.MacKeychainStore().Retrieve(null));
		}

		[Fact]
		public void LinuxSecretStore_Store_NullKey_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new KeePass.Platform.Unix.Linux.LinuxSecretStore()
					.Store(null, TestFixtures.CredentialTestSecret));
		}

		[Fact]
		public void LinuxSecretStore_Store_EmptyKey_Throws()
		{
			Assert.Throws<ArgumentException>(() =>
				new KeePass.Platform.Unix.Linux.LinuxSecretStore()
					.Store(string.Empty, TestFixtures.CredentialTestSecret));
		}

		[Fact]
		public void LinuxSecretStore_Store_NullSecret_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new KeePass.Platform.Unix.Linux.LinuxSecretStore()
					.Store("key", null));
		}

		[Fact]
		public void LinuxSecretStore_Store_EmptySecret_Throws()
		{
			Assert.Throws<ArgumentException>(() =>
				new KeePass.Platform.Unix.Linux.LinuxSecretStore()
					.Store("key", new byte[0]));
		}

		[Fact]
		public void LinuxSecretStore_Retrieve_NullKey_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new KeePass.Platform.Unix.Linux.LinuxSecretStore().Retrieve(null));
		}

		// ── macOS Keychain round-trip (macOS CI only) ─────────────────────

		[Fact]
		public void MacKeychainStore_IsSupported_IsTrueOnMac()
		{
			if(!TestFixtures.IsMacOS) return;
			Assert.True(new KeePass.Platform.Unix.Mac.MacKeychainStore().IsSupported);
		}

		[Fact]
		public void MacKeychainStore_StoreRetrieveDelete_RoundTrips()
		{
			if(!TestFixtures.IsMacOS) return;

			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			try
			{
				store.Store(UniqueKey, TestFixtures.CredentialTestSecret);
				byte[] retrieved = store.Retrieve(UniqueKey);
				Assert.NotNull(retrieved);
				Assert.Equal(TestFixtures.CredentialTestSecret, retrieved);
			}
			finally
			{
				try { store.Delete(UniqueKey); } catch { }
			}
		}

		[Fact]
		public void MacKeychainStore_Retrieve_NonExistentKey_ReturnsNull()
		{
			if(!TestFixtures.IsMacOS) return;
			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			byte[] result = store.Retrieve("KeePass.Tests.NonExistent." + Guid.NewGuid());
			Assert.Null(result);
		}

		[Fact]
		public void MacKeychainStore_Delete_MissingKey_DoesNotThrow()
		{
			if(!TestFixtures.IsMacOS) return;
			new KeePass.Platform.Unix.Mac.MacKeychainStore()
				.Delete("KeePass.Tests.Missing." + Guid.NewGuid());
		}

		// ── Linux Secret Service round-trip (Linux CI only) ───────────────

		[Fact]
		public void LinuxSecretStore_IsAvailable_WhenSecretToolInstalled()
		{
			if(!TestFixtures.IsLinux) return;
			// IsSupported returns true iff secret-tool is on PATH.
			// The test just verifies it doesn't throw.
			bool _ = new KeePass.Platform.Unix.Linux.LinuxSecretStore().IsSupported;
		}

		[Fact]
		public void LinuxSecretStore_StoreRetrieveDelete_RoundTrips()
		{
			if(!TestFixtures.IsLinux) return;
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			if(!store.IsSupported) return; // secret-tool not installed

			try
			{
				store.Store(UniqueKey, TestFixtures.CredentialTestSecret);
				byte[] retrieved = store.Retrieve(UniqueKey);
				Assert.NotNull(retrieved);
				Assert.Equal(TestFixtures.CredentialTestSecret, retrieved);
			}
			finally
			{
				try { store.Delete(UniqueKey); } catch { }
			}
		}

		[Fact]
		public void LinuxSecretStore_Retrieve_NonExistentKey_ReturnsNull()
		{
			if(!TestFixtures.IsLinux) return;
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			if(!store.IsSupported) return;

			byte[] result = store.Retrieve("KeePass.Tests.NonExistent." + Guid.NewGuid());
			Assert.Null(result);
		}

		[Fact]
		public void LinuxSecretStore_Delete_MissingKey_DoesNotThrow()
		{
			if(!TestFixtures.IsLinux) return;
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			if(!store.IsSupported) return;

			store.Delete("KeePass.Tests.Missing." + Guid.NewGuid());
		}
	}
}
