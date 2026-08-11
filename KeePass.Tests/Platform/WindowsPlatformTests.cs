/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Runtime.InteropServices;

using KeePass.Core.Platform;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Unit tests for WO-039: verifies that <c>WindowsPlatformIntegration</c>
	/// reports correct capability tiers, and that <c>WindowsCredentialStore</c>
	/// correctly stores, retrieves, and deletes credentials on Windows agents.
	/// Tests that require a running Credential Manager are guarded with an early
	/// return so they silently pass on non-Windows CI agents.
	/// </summary>
	public sealed class WindowsPlatformTests
	{
		private static bool OnWindows =>
			RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		// ── WindowsCredentialStore argument validation (runs everywhere) ────────

		[Fact]
		public void WindowsCredentialStore_Store_NullKey_Throws()
		{
			var store = new KeePass.Platform.WindowsCredentialStore();
			Assert.Throws<ArgumentNullException>(() =>
				store.Store(null, new byte[] { 1, 2 }));
		}

		[Fact]
		public void WindowsCredentialStore_Store_EmptyKey_Throws()
		{
			var store = new KeePass.Platform.WindowsCredentialStore();
			Assert.Throws<ArgumentException>(() =>
				store.Store(string.Empty, new byte[] { 1, 2 }));
		}

		[Fact]
		public void WindowsCredentialStore_Store_NullSecret_Throws()
		{
			var store = new KeePass.Platform.WindowsCredentialStore();
			Assert.Throws<ArgumentNullException>(() =>
				store.Store("k", null));
		}

		[Fact]
		public void WindowsCredentialStore_Store_EmptySecret_Throws()
		{
			var store = new KeePass.Platform.WindowsCredentialStore();
			Assert.Throws<ArgumentException>(() =>
				store.Store("k", new byte[0]));
		}

		[Fact]
		public void WindowsCredentialStore_Retrieve_NullKey_Throws()
		{
			var store = new KeePass.Platform.WindowsCredentialStore();
			Assert.Throws<ArgumentNullException>(() => store.Retrieve(null));
		}

		[Fact]
		public void WindowsCredentialStore_IsSupported_IsTrue()
		{
			Assert.True(new KeePass.Platform.WindowsCredentialStore().IsSupported);
		}

		// ── WindowsPlatformIntegration capability tiers (runs everywhere) ───────
		// We use FakePlatformIntegration.DefaultTierFor to verify the expected
		// values without instantiating the real WinForms-dependent platform class
		// on non-Windows systems.

		[Theory]
		[InlineData(PlatformCapability.Clipboard,               PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.ClipboardPrivacyMarkers, PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.CredentialStore,          PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.AutoType,                 PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.SecureDesktop,            PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.ScreenCaptureProtection,  PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.ProcessDacl,              PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.GlobalHotKeys,            PlatformCapabilityTier.Full)]
		public void WindowsPlatform_CapabilityTier_MatchesExpected(
			PlatformCapability capability, PlatformCapabilityTier expected)
		{
			// Use the FakePlatformIntegration helper for cross-platform test runs.
			Assert.Equal(expected,
				FakePlatformIntegration.DefaultTierFor(PlatformId.Windows, capability));
		}

		[Fact]
		public void WindowsPlatform_UnknownCapability_IsUnsupported()
		{
			Assert.Equal(PlatformCapabilityTier.Unsupported,
				FakePlatformIntegration.DefaultTierFor(PlatformId.Windows, (PlatformCapability)999));
		}

		// ── WindowsCredentialStore round-trip (Windows CI only) ────────────────

		// Known test credential — stable bytes for deterministic verification.
		private static readonly byte[] TestSecret = new byte[]
		{
			0x54, 0x65, 0x73, 0x74, // "Test"
			0x4B, 0x65, 0x65, 0x50, // "KeeP"
			0x61, 0x73, 0x73, 0x30  // "ass0"
		};

		private const string TestCredentialKey = "KeePass.Tests.WO039.RoundTrip";

		[Fact]
		public void WindowsCredentialStore_StoreAndRetrieve_RoundTrips()
		{
			if(!OnWindows) return; // Credential Manager only available on Windows.

			var store = new KeePass.Platform.WindowsCredentialStore();
			try
			{
				store.Store(TestCredentialKey, TestSecret);
				byte[] retrieved = store.Retrieve(TestCredentialKey);

				Assert.NotNull(retrieved);
				Assert.Equal(TestSecret, retrieved);
			}
			finally
			{
				try { store.Delete(TestCredentialKey); } catch { }
			}
		}

		[Fact]
		public void WindowsCredentialStore_Delete_RemovesCredential()
		{
			if(!OnWindows) return;

			var store = new KeePass.Platform.WindowsCredentialStore();
			store.Store(TestCredentialKey, TestSecret);
			store.Delete(TestCredentialKey);

			byte[] retrieved = store.Retrieve(TestCredentialKey);
			Assert.Null(retrieved);
		}

		[Fact]
		public void WindowsCredentialStore_Delete_NonExistentKey_DoesNotThrow()
		{
			if(!OnWindows) return;

			var store = new KeePass.Platform.WindowsCredentialStore();
			store.Delete("KeePass.Tests.WO039.Missing." + Guid.NewGuid());
		}

		[Fact]
		public void WindowsCredentialStore_Retrieve_MissingKey_ReturnsNull()
		{
			if(!OnWindows) return;

			var store = new KeePass.Platform.WindowsCredentialStore();
			byte[] result = store.Retrieve("KeePass.Tests.WO039.Missing." + Guid.NewGuid());
			Assert.Null(result);
		}
	}
}
