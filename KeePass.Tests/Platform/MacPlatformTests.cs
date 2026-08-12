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
	/// Unit tests for WO-041: verifies that <c>MacPlatformIntegration</c>
	/// reports correct capability tiers, and that <c>MacKeychainStore</c>
	/// argument validation works on all platforms.
	/// Tests requiring the real macOS <c>security</c> CLI or <c>pbcopy</c>
	/// are guarded by an early return when not running on macOS.
	/// </summary>
	public sealed class MacPlatformTests
	{
		private static bool OnMacOS =>
			RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

		// ── MacPlatform capability tiers (verified via FakePlatformIntegration) ─

		[Theory]
		[InlineData(PlatformCapability.Clipboard,               PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.ClipboardPrivacyMarkers, PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.CredentialStore,          PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.AutoType,                 PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.SecureDesktop,            PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.ScreenCaptureProtection,  PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.ProcessDacl,              PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.GlobalHotKeys,            PlatformCapabilityTier.Unsupported)]
		public void MacPlatform_CapabilityTier_MatchesSpec(
			PlatformCapability capability, PlatformCapabilityTier expected)
		{
			Assert.Equal(expected,
				FakePlatformIntegration.DefaultTierFor(PlatformId.MacOS, capability));
		}

		[Fact]
		public void MacPlatform_UnknownCapability_IsUnsupported()
		{
			Assert.Equal(PlatformCapabilityTier.Unsupported,
				FakePlatformIntegration.DefaultTierFor(PlatformId.MacOS, (PlatformCapability)999));
		}

		// ── MacKeychainStore argument validation (runs on all platforms) ────────

		[Fact]
		public void MacKeychainStore_IsSupported_IsTrue()
		{
			Assert.True(new KeePass.Platform.Unix.Mac.MacKeychainStore().IsSupported);
		}

		[Fact]
		public void MacKeychainStore_Store_NullKey_Throws()
		{
			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			Assert.Throws<ArgumentNullException>(() =>
				store.Store(null, new byte[] { 1, 2 }));
		}

		[Fact]
		public void MacKeychainStore_Store_EmptyKey_Throws()
		{
			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			Assert.Throws<ArgumentException>(() =>
				store.Store(string.Empty, new byte[] { 1, 2 }));
		}

		[Fact]
		public void MacKeychainStore_Store_NullSecret_Throws()
		{
			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			Assert.Throws<ArgumentNullException>(() =>
				store.Store("key", null));
		}

		[Fact]
		public void MacKeychainStore_Store_EmptySecret_Throws()
		{
			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			Assert.Throws<ArgumentException>(() =>
				store.Store("key", new byte[0]));
		}

		[Fact]
		public void MacKeychainStore_Retrieve_NullKey_Throws()
		{
			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			Assert.Throws<ArgumentNullException>(() => store.Retrieve(null));
		}

		// ── MacKeychainStore round-trip (macOS CI only) ────────────────────────

		// Known test credential bytes — stable for deterministic verification.
		private static readonly byte[] TestSecret = new byte[]
		{
			0x4D, 0x61, 0x63, 0x4B, // "MacK"
			0x65, 0x79, 0x63, 0x68, // "eych"
			0x61, 0x69, 0x6E, 0x30  // "ain0"
		};

		private const string TestCredentialKey = "KeePass.Tests.WO041.RoundTrip";

		[Fact]
		public void MacKeychainStore_StoreAndRetrieve_RoundTrips()
		{
			if(!OnMacOS) return;

			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
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
		public void MacKeychainStore_Delete_RemovesCredential()
		{
			if(!OnMacOS) return;

			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			store.Store(TestCredentialKey, TestSecret);
			store.Delete(TestCredentialKey);

			byte[] retrieved = store.Retrieve(TestCredentialKey);
			Assert.Null(retrieved);
		}

		[Fact]
		public void MacKeychainStore_Delete_MissingKey_DoesNotThrow()
		{
			if(!OnMacOS) return;

			var store = new KeePass.Platform.Unix.Mac.MacKeychainStore();
			store.Delete("KeePass.Tests.WO041.Missing." + Guid.NewGuid());
		}

		// ── MacClipboardService round-trip (macOS CI only) ─────────────────────

		[Fact]
		public void MacClipboardService_IsSupported_IsTrue()
		{
			var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			Assert.True(svc.IsSupported);
		}

		[Fact]
		public void MacClipboardService_ExtendsClipboardServiceBase()
		{
			// Structural check: ensures WO-041 refactoring is in place.
			Assert.True(
				typeof(KeePass.Core.Platform.ClipboardServiceBase)
					.IsAssignableFrom(typeof(KeePass.Platform.Unix.Mac.MacClipboardService)),
				"MacClipboardService must extend ClipboardServiceBase.");
		}

		[Fact]
		public void MacClipboardService_SetAndGetText_RoundTrips()
		{
			if(!OnMacOS) return;

			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			svc.SetText("keepass-test-value");
			string retrieved = svc.GetText();
			Assert.Equal("keepass-test-value", retrieved);
		}

		[Fact]
		public void MacClipboardService_ClearIfOwner_WhenOwner_Clears()
		{
			if(!OnMacOS) return;

			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			svc.SetText("sensitive");
			svc.ClearIfOwner();
			string remaining = svc.GetText();
			Assert.True(string.IsNullOrEmpty(remaining),
				"Clipboard should have been cleared after ClearIfOwner.");
		}
	}
}
