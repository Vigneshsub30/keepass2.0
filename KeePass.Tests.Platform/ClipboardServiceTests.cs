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
	/// Characterization tests for <see cref="IClipboardService"/> implementations
	/// on each platform (WO-044).
	///
	/// Tests that invoke real CLI tools (pbcopy, xsel, xclip, wl-copy) are
	/// guarded by a platform check and return early when not on the expected OS
	/// or when the tool is not installed.
	/// </summary>
	public sealed class ClipboardServiceTests
	{
		// ── ClipboardServiceBase structural checks (all platforms) ─────────

		[Fact]
		public void MacClipboardService_ExtendsClipboardServiceBase()
		{
			Assert.True(
				typeof(ClipboardServiceBase).IsAssignableFrom(
					typeof(KeePass.Platform.Unix.Mac.MacClipboardService)));
		}

		[Fact]
		public void LinuxClipboardService_ExtendsClipboardServiceBase()
		{
			Assert.True(
				typeof(ClipboardServiceBase).IsAssignableFrom(
					typeof(KeePass.Platform.Unix.Linux.LinuxClipboardService)));
		}

		[Fact]
		public void MacClipboardService_IsSupported_IsTrueOnMac()
		{
			if(!TestFixtures.IsMacOS) return;
			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			Assert.True(svc.IsSupported);
		}

		// ── macOS clipboard round-trip (macOS CI only) ────────────────────

		[Fact]
		public void MacClipboard_SetAndGetText_RoundTrips()
		{
			if(!TestFixtures.IsMacOS) return;
			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			if(!svc.IsSupported) return;

			svc.SetText(TestFixtures.ClipboardTestText);
			Assert.Equal(TestFixtures.ClipboardTestText, svc.GetText());
		}

		[Fact]
		public void MacClipboard_Clear_EmptiesClipboard()
		{
			if(!TestFixtures.IsMacOS) return;
			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			if(!svc.IsSupported) return;

			svc.SetText(TestFixtures.ClipboardTestText);
			svc.Clear();
			Assert.True(string.IsNullOrEmpty(svc.GetText()));
		}

		[Fact]
		public void MacClipboard_ClearIfOwner_WhenOwner_Clears()
		{
			if(!TestFixtures.IsMacOS) return;
			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			if(!svc.IsSupported) return;

			svc.SetText(TestFixtures.ClipboardSimpleText);
			svc.ClearIfOwner();
			Assert.True(string.IsNullOrEmpty(svc.GetText()));
		}

		[Fact]
		public void MacClipboard_ClearIfOwner_WhenNotOwner_DoesNotClear()
		{
			if(!TestFixtures.IsMacOS) return;
			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			if(!svc.IsSupported) return;

			// Set text via a second instance so svc does not hold ownership.
			using (var other = new KeePass.Platform.Unix.Mac.MacClipboardService())
				other.SetText(TestFixtures.ClipboardSimpleText);

			svc.ClearIfOwner(); // svc never set anything, so no ownership.
			Assert.Equal(TestFixtures.ClipboardSimpleText, svc.GetText());
		}

		[Fact]
		public void MacClipboard_SetText_NullThrows()
		{
			using var svc = new KeePass.Platform.Unix.Mac.MacClipboardService();
			Assert.Throws<ArgumentNullException>(() => svc.SetText(null));
		}

		// ── Linux clipboard round-trip (Linux CI only) ─────────────────────

		[Fact]
		public void LinuxClipboard_SetAndGetText_RoundTrips()
		{
			if(!TestFixtures.IsLinux) return;
			using var svc = new KeePass.Platform.Unix.Linux.LinuxClipboardService();
			if(!svc.IsSupported) return; // no clipboard helper on this agent

			svc.SetText(TestFixtures.ClipboardTestText);
			Assert.Equal(TestFixtures.ClipboardTestText, svc.GetText());
		}

		[Fact]
		public void LinuxClipboard_Clear_EmptiesClipboard()
		{
			if(!TestFixtures.IsLinux) return;
			using var svc = new KeePass.Platform.Unix.Linux.LinuxClipboardService();
			if(!svc.IsSupported) return;

			svc.SetText(TestFixtures.ClipboardTestText);
			svc.Clear();
			Assert.True(string.IsNullOrEmpty(svc.GetText()));
		}

		[Fact]
		public void LinuxClipboard_ClearIfOwner_WhenOwner_Clears()
		{
			if(!TestFixtures.IsLinux) return;
			using var svc = new KeePass.Platform.Unix.Linux.LinuxClipboardService();
			if(!svc.IsSupported) return;

			svc.SetText(TestFixtures.ClipboardSimpleText);
			svc.ClearIfOwner();
			Assert.True(string.IsNullOrEmpty(svc.GetText()));
		}

		[Fact]
		public void LinuxClipboard_SetText_NullThrows()
		{
			using var svc = new KeePass.Platform.Unix.Linux.LinuxClipboardService();
			Assert.Throws<ArgumentNullException>(() => svc.SetText(null));
		}
	}
}
