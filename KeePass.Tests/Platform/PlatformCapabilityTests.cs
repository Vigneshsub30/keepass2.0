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
	/// Unit tests for WO-038: verifies the <see cref="PlatformCapability"/> and
	/// <see cref="PlatformCapabilityTier"/> enums are correctly defined and that
	/// <see cref="IPlatformIntegration.GetCapabilityTier"/> behaves correctly for
	/// <see cref="FakePlatformIntegration"/> and <see cref="FallbackPlatformIntegration"/>.
	/// </summary>
	public sealed class PlatformCapabilityTests
	{
		// ── Enum completeness ──────────────────────────────────────────────────

		[Fact]
		public void PlatformCapability_ContainsAllRequiredValues()
		{
			// All values mandated by the WO-038 acceptance criteria must exist.
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.Clipboard));
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.ClipboardPrivacyMarkers));
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.CredentialStore));
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.AutoType));
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.SecureDesktop));
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.ScreenCaptureProtection));
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.ProcessDacl));
			Assert.True(Enum.IsDefined(typeof(PlatformCapability), PlatformCapability.GlobalHotKeys));
		}

		[Fact]
		public void PlatformCapabilityTier_ContainsAllRequiredValues()
		{
			Assert.True(Enum.IsDefined(typeof(PlatformCapabilityTier), PlatformCapabilityTier.Full));
			Assert.True(Enum.IsDefined(typeof(PlatformCapabilityTier), PlatformCapabilityTier.Partial));
			Assert.True(Enum.IsDefined(typeof(PlatformCapabilityTier), PlatformCapabilityTier.Unsupported));
		}

		// ── FakePlatformIntegration construction ───────────────────────────────

		[Fact]
		public void FakePlatformIntegration_CanBeConstructed_Windows()
		{
			var fake = new FakePlatformIntegration(PlatformId.Windows);
			Assert.Equal(PlatformId.Windows, fake.PlatformId);
			Assert.True(fake.SupportsAlwaysOnTop);
			Assert.False(fake.RequiresWindowMinSizeEnforcement);
		}

		[Fact]
		public void FakePlatformIntegration_CanBeConstructed_Linux()
		{
			var fake = new FakePlatformIntegration(PlatformId.Linux);
			Assert.Equal(PlatformId.Linux, fake.PlatformId);
			Assert.False(fake.SupportsAlwaysOnTop);
			Assert.True(fake.RequiresWindowMinSizeEnforcement);
		}

		[Fact]
		public void FakePlatformIntegration_CanBeConstructed_MacOS()
		{
			var fake = new FakePlatformIntegration(PlatformId.MacOS);
			Assert.Equal(PlatformId.MacOS, fake.PlatformId);
		}

		[Fact]
		public void FakePlatformIntegration_SubServicesAreNotNull()
		{
			var fake = new FakePlatformIntegration();
			Assert.NotNull(fake.Clipboard);
			Assert.NotNull(fake.CredentialStore);
			Assert.NotNull(fake.AutoType);
			Assert.NotNull(fake.ScreenProtection);
		}

		// ── GetCapabilityTier defaults ─────────────────────────────────────────

		[Fact]
		public void FakePlatformIntegration_Windows_ClipboardFull()
		{
			var fake = new FakePlatformIntegration(PlatformId.Windows);
			Assert.Equal(PlatformCapabilityTier.Full,
				fake.GetCapabilityTier(PlatformCapability.Clipboard));
		}

		[Fact]
		public void FakePlatformIntegration_Windows_AutoTypeFull()
		{
			var fake = new FakePlatformIntegration(PlatformId.Windows);
			Assert.Equal(PlatformCapabilityTier.Full,
				fake.GetCapabilityTier(PlatformCapability.AutoType));
		}

		[Fact]
		public void FakePlatformIntegration_Linux_AutoTypeUnsupported()
		{
			var fake = new FakePlatformIntegration(PlatformId.Linux);
			Assert.Equal(PlatformCapabilityTier.Unsupported,
				fake.GetCapabilityTier(PlatformCapability.AutoType));
		}

		[Fact]
		public void FakePlatformIntegration_MacOS_CredentialStoreFull()
		{
			var fake = new FakePlatformIntegration(PlatformId.MacOS);
			Assert.Equal(PlatformCapabilityTier.Full,
				fake.GetCapabilityTier(PlatformCapability.CredentialStore));
		}

		[Fact]
		public void FakePlatformIntegration_UnknownCapability_ReturnsUnsupported()
		{
			var fake = new FakePlatformIntegration(PlatformId.Windows);
			// Cast an out-of-range value to simulate a future capability.
			var future = (PlatformCapability)999;
			Assert.Equal(PlatformCapabilityTier.Unsupported,
				fake.GetCapabilityTier(future));
		}

		// ── CapabilityOverrides ────────────────────────────────────────────────

		[Fact]
		public void FakePlatformIntegration_CapabilityOverride_TakesPrecedence()
		{
			var fake = new FakePlatformIntegration(PlatformId.Linux);
			fake.CapabilityOverrides[PlatformCapability.AutoType] = PlatformCapabilityTier.Full;

			Assert.Equal(PlatformCapabilityTier.Full,
				fake.GetCapabilityTier(PlatformCapability.AutoType));
		}

		// ── FallbackPlatformIntegration.GetCapabilityTier ─────────────────────

		[Fact]
		public void FallbackPlatformIntegration_AlwaysReturnsUnsupported()
		{
			IPlatformIntegration fallback = FallbackPlatformIntegration.Instance;

			foreach(PlatformCapability cap in Enum.GetValues(typeof(PlatformCapability)))
			{
				Assert.Equal(PlatformCapabilityTier.Unsupported,
					fallback.GetCapabilityTier(cap));
			}
		}

		// ── FakePlatformIntegration sub-service no-ops ─────────────────────────

		[Fact]
		public void FakePlatformIntegration_Clipboard_SetAndGetRoundTrips()
		{
			var fake = new FakePlatformIntegration();
			fake.Clipboard.SetText("hello");
			Assert.Equal("hello", fake.Clipboard.GetText());
		}

		[Fact]
		public void FakePlatformIntegration_CredentialStore_StoreAndRetrieve()
		{
			var fake = new FakePlatformIntegration();
			byte[] secret = new byte[] { 1, 2, 3 };
			fake.CredentialStore.Store("key1", secret);
			Assert.Equal(secret, fake.CredentialStore.Retrieve("key1"));
		}

		[Fact]
		public void FakePlatformIntegration_CredentialStore_RetrieveMissing_ReturnsNull()
		{
			var fake = new FakePlatformIntegration();
			Assert.Null(fake.CredentialStore.Retrieve("nonexistent"));
		}

		// ── FakePlatformIntegration.DefaultTierFor static helper ───────────────

		[Theory]
		[InlineData(PlatformId.Windows, PlatformCapability.Clipboard,      PlatformCapabilityTier.Full)]
		[InlineData(PlatformId.Windows, PlatformCapability.AutoType,       PlatformCapabilityTier.Full)]
		[InlineData(PlatformId.Windows, PlatformCapability.SecureDesktop,  PlatformCapabilityTier.Full)]
		[InlineData(PlatformId.MacOS,   PlatformCapability.Clipboard,      PlatformCapabilityTier.Full)]
		[InlineData(PlatformId.MacOS,   PlatformCapability.AutoType,       PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformId.Linux,   PlatformCapability.AutoType,       PlatformCapabilityTier.Unsupported)]
		[InlineData(PlatformId.Linux,   PlatformCapability.CredentialStore,PlatformCapabilityTier.Partial)]
		public void DefaultTierFor_ReturnsExpectedTier(
			PlatformId platformId, PlatformCapability capability, PlatformCapabilityTier expected)
		{
			Assert.Equal(expected,
				FakePlatformIntegration.DefaultTierFor(platformId, capability));
		}
	}
}
