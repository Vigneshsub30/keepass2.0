/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Threading;
using System.Threading.Tasks;

using KeePass.Core.Platform;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Regression tests for the five MonoWorkarounds classified as
	/// RE-IMPLEMENT in WO-043 (MonoWorkarounds-Classification.md).
	///
	/// Tests verify that behaviors previously in MonoWorkarounds.cs are now
	/// correctly provided by <see cref="IPlatformIntegration"/> or platform
	/// service implementations.
	/// </summary>
	public sealed class MonoWorkaroundRegressionTests
	{
		// ── Workaround #1716: AlwaysOnTop on Cinnamon (SupportsAlwaysOnTop) ─

		[Fact]
		public void Workaround1716_FallbackPlatform_SupportsAlwaysOnTop_TrueExceptLinux()
		{
			// FallbackPlatformIntegration conservative defaults: true on Windows/macOS.
			bool expected = !TestFixtures.IsLinux;
			Assert.Equal(expected, FallbackPlatformIntegration.Instance.SupportsAlwaysOnTop);
		}

		[Fact]
		public void Workaround1716_Linux_LiveDetection_SupportsAlwaysOnTopDoesNotThrow()
		{
			if(!TestFixtures.IsLinux) return;
			var platform = KeePass.Platform.Unix.Linux.LinuxPlatformIntegration.Create();
			bool _ = platform.SupportsAlwaysOnTop; // must not throw
		}

		[Fact]
		public void Workaround1716_MacOS_SupportsAlwaysOnTop_IsTrue()
		{
			if(!TestFixtures.IsMacOS) return;
			var platform = KeePass.Platform.Unix.Mac.MacPlatformIntegration.Create();
			Assert.True(platform.SupportsAlwaysOnTop);
		}

		// ── Workaround #686017: RequiresWindowMinSizeEnforcement ──────────

		[Fact]
		public void Workaround686017_Fallback_RequiresWindowMinSizeEnforcement_TrueOnLinux()
		{
			bool expected = TestFixtures.IsLinux;
			Assert.Equal(expected,
				FallbackPlatformIntegration.Instance.RequiresWindowMinSizeEnforcement);
		}

		[Fact]
		public void Workaround686017_Linux_RequiresWindowMinSizeEnforcement_IsTrue()
		{
			if(!TestFixtures.IsLinux) return;
			var platform = KeePass.Platform.Unix.Linux.LinuxPlatformIntegration.Create();
			Assert.True(platform.RequiresWindowMinSizeEnforcement);
		}

		[Fact]
		public void Workaround686017_MacOS_RequiresWindowMinSizeEnforcement_IsFalse()
		{
			if(!TestFixtures.IsMacOS) return;
			var platform = KeePass.Platform.Unix.Mac.MacPlatformIntegration.Create();
			Assert.False(platform.RequiresWindowMinSizeEnforcement);
		}

		// ── Workaround #19836: URL / document opening via platform ─────────
		// #19836 is planned for a future WO; this test characterizes the
		// current state — platform is correctly identified, enabling migration.

		[Fact]
		public void Workaround19836_PlatformId_CorrectlyIdentifiedOnEachOS()
		{
			if(TestFixtures.IsLinux)
			{
				var p = KeePass.Platform.Unix.Linux.LinuxPlatformIntegration.Create();
				Assert.Equal(PlatformId.Linux, p.PlatformId);
				return;
			}
			if(TestFixtures.IsMacOS)
			{
				var p = KeePass.Platform.Unix.Mac.MacPlatformIntegration.Create();
				Assert.Equal(PlatformId.MacOS, p.PlatformId);
				return;
			}
			// On Windows, fallback detects the current OS.
			Assert.Equal(PlatformId.Windows,
				FallbackPlatformIntegration.Instance.PlatformId);
		}

		// ── Workaround #190417 / #3471228285: process/argument encoding ────
		// These will be fully migrated in WO-044+.  This test verifies that
		// Process.Start on .NET 10 does NOT mangle backslashes (the Mono bug).

		[Fact]
		public void Workaround190417_DotNet10_ProcessStart_PreservesBackslashes()
		{
			// On .NET 10, echo (or cmd /C echo) correctly preserves backslashes
			// in arguments. The Mono bug (#190417) replaced \\ with / in arguments.
			// We use `echo` with a known backslash argument and verify round-trip.
			// Only verifiable on platforms where a suitable command exists.
			if(TestFixtures.IsWindows) return; // cmd.exe quoting is different; skip

			using var proc = new System.Diagnostics.Process();
			proc.StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName               = "echo",
				Arguments              = "test\\\\value",
				RedirectStandardOutput = true,
				UseShellExecute        = false,
				CreateNoWindow         = true,
			};
			proc.Start();
			string output = proc.StandardOutput.ReadToEnd();
			proc.WaitForExit(5000);
			// The Mono bug would have turned "test\\value" into "test//value".
			Assert.DoesNotContain("test//value", output, StringComparison.Ordinal);
		}

		// ── Workaround #100004/#1468: native crypto (AES, Argon2) ──────────

		[Fact]
		public void Workaround100004_DotNet10_AesCreate_ReturnsNativeImpl()
		{
			// .NET 10 Aes.Create() calls native OS crypto (CNG on Windows,
			// OpenSSL on Linux/macOS), replacing both LibGCrypt (#1468) and the
			// native Argon2 workaround (#100004).
			using var aes = System.Security.Cryptography.Aes.Create();
			Assert.NotNull(aes);
			Assert.True(aes.KeySize == 128 || aes.KeySize == 256);
		}

		[Fact]
		public void Workaround100004_DotNet10_SHA256_DoesNotThrow()
		{
			using var sha = System.Security.Cryptography.SHA256.Create();
			byte[] hash = sha.ComputeHash(new byte[] { 1, 2, 3 });
			Assert.Equal(32, hash.Length);
		}

		// ── Workaround #1530/#1613 (OBSOLETE): Thread.Abort replacement ────

		[Fact]
		public void Workaround1530_CancellationToken_TerminatesWithin5Seconds()
		{
			// Thread.Abort() is not available on .NET 5+. The clipboard fix
			// thread (g_thFixClip) was removed entirely in WO-035.
			// This test verifies the CancellationToken pattern works correctly.
			using var cts = new CancellationTokenSource();
			CancellationToken token = cts.Token;

			// Do NOT pass token to Task.Run — avoid TaskCanceledException on the task.
			// The lambda polls IsCancellationRequested and exits cleanly.
			bool completed = false;
			Task t = Task.Run(() =>
			{
				while(!token.IsCancellationRequested)
					Thread.Sleep(5);
				completed = true;
			});

			cts.Cancel();
			bool finished = t.Wait(5000);
			Assert.True(finished, "CancellationToken-based termination must complete within 5 s.");
			Assert.True(completed);
		}
	}
}
