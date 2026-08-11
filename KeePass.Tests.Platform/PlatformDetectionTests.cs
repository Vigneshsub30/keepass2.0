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
	/// Characterization tests for platform detection via
	/// <see cref="RuntimeInformation"/> and <see cref="IPlatformIntegration"/>
	/// (WO-044).  All tests run cross-platform.
	/// </summary>
	public sealed class PlatformDetectionTests
	{
		// ── RuntimeInformation characterization ──────────────────────────────

		[Fact]
		public void RuntimeInformation_ExactlyOnePlatform_IsIdentified()
		{
			int count = 0;
			if(TestFixtures.IsWindows) count++;
			if(TestFixtures.IsMacOS)   count++;
			if(TestFixtures.IsLinux)   count++;
			Assert.Equal(1, count);
		}

		[Fact]
		public void RuntimeInformation_IsUnix_TrueOnMacAndLinux()
		{
			bool expected = TestFixtures.IsMacOS || TestFixtures.IsLinux;
			Assert.Equal(expected, TestFixtures.IsUnix);
		}

		[Fact]
		public void RuntimeInformation_OSArchitecture_IsRecognized()
		{
			// Must be one of the known Architecture enum values.
			Architecture arch = RuntimeInformation.OSArchitecture;
			Assert.True(
				arch == Architecture.X64 ||
				arch == Architecture.X86 ||
				arch == Architecture.Arm64 ||
				arch == Architecture.Arm,
				$"Unexpected OSArchitecture: {arch}");
		}

		[Fact]
		public void RuntimeInformation_FrameworkDescription_ContainsDotNet()
		{
			string description = RuntimeInformation.FrameworkDescription;
			Assert.False(string.IsNullOrWhiteSpace(description));
			Assert.Contains(".NET", description, StringComparison.OrdinalIgnoreCase);
		}

		// ── IPlatformIntegration platform ID characterization ────────────────

		[Fact]
		public void FallbackPlatformIntegration_PlatformId_ReflectsCurrentOS()
		{
			// FallbackPlatformIntegration detects the current OS at runtime.
			PlatformId pid = FallbackPlatformIntegration.Instance.PlatformId;
			if(TestFixtures.IsWindows) Assert.Equal(PlatformId.Windows, pid);
			else if(TestFixtures.IsMacOS) Assert.Equal(PlatformId.MacOS, pid);
			else Assert.Equal(PlatformId.Linux, pid);
		}

		[Fact]
		public void FallbackPlatformIntegration_AllCapabilities_AreUnsupported()
		{
			foreach(PlatformCapability cap in Enum.GetValues(typeof(PlatformCapability)))
			{
				Assert.Equal(PlatformCapabilityTier.Unsupported,
					FallbackPlatformIntegration.Instance.GetCapabilityTier(cap));
			}
		}

		[Fact]
		public void FallbackPlatformIntegration_SupportsAlwaysOnTop_TrueExceptLinux()
		{
			// Conservative safe defaults: true on Windows/macOS, false on Linux.
			bool expected = !TestFixtures.IsLinux;
			Assert.Equal(expected, FallbackPlatformIntegration.Instance.SupportsAlwaysOnTop);
		}

		[Fact]
		public void FallbackPlatformIntegration_RequiresWindowMinSizeEnforcement_TrueOnLinux()
		{
			// Conservative safe defaults: true on Linux, false elsewhere.
			bool expected = TestFixtures.IsLinux;
			Assert.Equal(expected,
				FallbackPlatformIntegration.Instance.RequiresWindowMinSizeEnforcement);
		}

		// ── Linux platform detection (Linux CI only) ─────────────────────────

		[Fact]
		public void LinuxPlatformIntegration_PlatformId_IsLinux()
		{
			if(!TestFixtures.IsLinux) return;
			var platform = KeePass.Platform.Unix.Linux.LinuxPlatformIntegration.Create();
			Assert.Equal(PlatformId.Linux, platform.PlatformId);
		}

		[Fact]
		public void LinuxPlatformIntegration_RequiresWindowMinSizeEnforcement_IsTrue()
		{
			if(!TestFixtures.IsLinux) return;
			var platform = KeePass.Platform.Unix.Linux.LinuxPlatformIntegration.Create();
			Assert.True(platform.RequiresWindowMinSizeEnforcement);
		}

		// ── macOS platform detection (macOS CI only) ─────────────────────────

		[Fact]
		public void MacPlatformIntegration_PlatformId_IsMacOS()
		{
			if(!TestFixtures.IsMacOS) return;
			var platform = KeePass.Platform.Unix.Mac.MacPlatformIntegration.Create();
			Assert.Equal(PlatformId.MacOS, platform.PlatformId);
		}

		[Fact]
		public void MacPlatformIntegration_SupportsAlwaysOnTop_IsTrue()
		{
			if(!TestFixtures.IsMacOS) return;
			var platform = KeePass.Platform.Unix.Mac.MacPlatformIntegration.Create();
			Assert.True(platform.SupportsAlwaysOnTop);
		}

		[Fact]
		public void MacPlatformIntegration_RequiresWindowMinSizeEnforcement_IsFalse()
		{
			if(!TestFixtures.IsMacOS) return;
			var platform = KeePass.Platform.Unix.Mac.MacPlatformIntegration.Create();
			Assert.False(platform.RequiresWindowMinSizeEnforcement);
		}
	}
}
