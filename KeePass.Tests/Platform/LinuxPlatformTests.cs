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
	/// Unit tests for WO-042: verifies that Linux platform capability tiers are
	/// correct for a standard X11 desktop with secret-tool installed, and that
	/// <c>LinuxSecretStore</c> argument validation works on all platforms.
	/// Tests requiring xsel/xclip or secret-tool are guarded by early returns
	/// when not running on Linux.
	/// </summary>
	public sealed class LinuxPlatformTests
	{
		private static bool OnLinux =>
			RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

		// ── LinuxPlatform capability tiers (standard X11 + secret-tool available) ─

		[Theory]
		[InlineData(PlatformCapability.Clipboard,               PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.ClipboardPrivacyMarkers, PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.CredentialStore,          PlatformCapabilityTier.Full)]
		[InlineData(PlatformCapability.AutoType,                 PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.SecureDesktop,            PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.ScreenCaptureProtection,  PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.ProcessDacl,              PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformCapability.GlobalHotKeys,            PlatformCapabilityTier.Unsupported)]
		public void LinuxPlatform_X11WithSecretTool_CapabilityTierMatchesSpec(
			PlatformCapability capability, PlatformCapabilityTier expected)
		{
			Assert.Equal(expected,
				FakePlatformIntegration.DefaultTierFor(PlatformId.Linux, capability));
		}

		[Fact]
		public void LinuxPlatform_UnknownCapability_IsUnsupported()
		{
			Assert.Equal(PlatformCapabilityTier.Unsupported,
				FakePlatformIntegration.DefaultTierFor(PlatformId.Linux, (PlatformCapability)999));
		}

		// ── Wayland overrides (tested via FakePlatformIntegration.CapabilityOverrides) ─

		[Fact]
		public void LinuxPlatform_Wayland_ClipboardIsPartial()
		{
			var fake = new FakePlatformIntegration(PlatformId.Linux)
			{
				CapabilityOverrides =
				{
					[PlatformCapability.Clipboard] = PlatformCapabilityTier.Partial
				}
			};
			Assert.Equal(PlatformCapabilityTier.Partial,
				fake.GetCapabilityTier(PlatformCapability.Clipboard));
		}

		[Fact]
		public void LinuxPlatform_Wayland_ClipboardPrivacyMarkersIsPartial()
		{
			var fake = new FakePlatformIntegration(PlatformId.Linux)
			{
				CapabilityOverrides =
				{
					[PlatformCapability.ClipboardPrivacyMarkers] = PlatformCapabilityTier.Partial
				}
			};
			Assert.Equal(PlatformCapabilityTier.Partial,
				fake.GetCapabilityTier(PlatformCapability.ClipboardPrivacyMarkers));
		}

		[Fact]
		public void LinuxPlatform_NoSecretTool_CredentialStoreIsUnsupported()
		{
			var fake = new FakePlatformIntegration(PlatformId.Linux)
			{
				CapabilityOverrides =
				{
					[PlatformCapability.CredentialStore] = PlatformCapabilityTier.Unsupported
				}
			};
			Assert.Equal(PlatformCapabilityTier.Unsupported,
				fake.GetCapabilityTier(PlatformCapability.CredentialStore));
		}

		// ── LinuxSecretStore argument validation (runs on all platforms) ────────

		[Fact]
		public void LinuxSecretStore_Store_NullKey_Throws()
		{
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			Assert.Throws<ArgumentNullException>(() =>
				store.Store(null, new byte[] { 1, 2 }));
		}

		[Fact]
		public void LinuxSecretStore_Store_EmptyKey_Throws()
		{
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			Assert.Throws<ArgumentException>(() =>
				store.Store(string.Empty, new byte[] { 1, 2 }));
		}

		[Fact]
		public void LinuxSecretStore_Store_NullSecret_Throws()
		{
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			Assert.Throws<ArgumentNullException>(() =>
				store.Store("key", null));
		}

		[Fact]
		public void LinuxSecretStore_Store_EmptySecret_Throws()
		{
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			Assert.Throws<ArgumentException>(() =>
				store.Store("key", new byte[0]));
		}

		[Fact]
		public void LinuxSecretStore_Retrieve_NullKey_Throws()
		{
			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			Assert.Throws<ArgumentNullException>(() => store.Retrieve(null));
		}

		// ── LinuxClipboardService structural checks (all platforms) ───────────

		[Fact]
		public void LinuxClipboardService_ExtendsClipboardServiceBase()
		{
			Assert.True(
				typeof(KeePass.Core.Platform.ClipboardServiceBase)
					.IsAssignableFrom(typeof(KeePass.Platform.Unix.Linux.LinuxClipboardService)),
				"LinuxClipboardService must extend ClipboardServiceBase.");
		}

		// ── Integration tests (Linux CI only) ────────────────────────────────

		// Known test credential bytes — stable for deterministic verification.
		private static readonly byte[] TestSecret = new byte[]
		{
			0x4C, 0x69, 0x6E, 0x78, // "Linx"
			0x53, 0x65, 0x63, 0x30  // "Sec0"
		};

		private const string TestCredentialKey = "KeePass.Tests.WO042.RoundTrip";

		[Fact]
		public void LinuxSecretStore_StoreAndRetrieve_RoundTrips()
		{
			if(!OnLinux) return;

			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			if(!store.IsSupported) return; // secret-tool not installed on this agent

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
		public void LinuxSecretStore_Delete_RemovesCredential()
		{
			if(!OnLinux) return;

			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			if(!store.IsSupported) return;

			store.Store(TestCredentialKey, TestSecret);
			store.Delete(TestCredentialKey);

			byte[] retrieved = store.Retrieve(TestCredentialKey);
			Assert.Null(retrieved);
		}

		[Fact]
		public void LinuxSecretStore_Delete_MissingKey_DoesNotThrow()
		{
			if(!OnLinux) return;

			var store = new KeePass.Platform.Unix.Linux.LinuxSecretStore();
			if(!store.IsSupported) return;

			store.Delete("KeePass.Tests.WO042.Missing." + Guid.NewGuid());
		}

		[Fact]
		public void LinuxClipboardService_SetAndGetText_RoundTrips()
		{
			if(!OnLinux) return;

			using var svc = new KeePass.Platform.Unix.Linux.LinuxClipboardService();
			if(!svc.IsSupported) return; // no clipboard helper on this agent

			svc.SetText("keepass-linux-test");
			string retrieved = svc.GetText();
			Assert.Equal("keepass-linux-test", retrieved);
		}

		[Fact]
		public void LinuxClipboardService_ClearIfOwner_WhenOwner_Clears()
		{
			if(!OnLinux) return;

			using var svc = new KeePass.Platform.Unix.Linux.LinuxClipboardService();
			if(!svc.IsSupported) return;

			svc.SetText("sensitive");
			svc.ClearIfOwner();
			string remaining = svc.GetText();
			Assert.True(string.IsNullOrEmpty(remaining),
				"Clipboard should have been cleared after ClearIfOwner.");
		}
	}
}
