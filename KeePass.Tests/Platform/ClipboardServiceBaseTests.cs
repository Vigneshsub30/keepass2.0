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

using KeePass.Core.Platform;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Unit tests for WO-040: verifies <see cref="ClipboardServiceBase"/>
	/// ownership-hash semantics and auto-clear timer behavior.
	/// </summary>
	public sealed class ClipboardServiceBaseTests
	{
		// ── SetText / GetText ──────────────────────────────────────────────────

		[Fact]
		public void SetText_StoresTextInClipboard()
		{
			var svc = new TestableClipboardService();
			svc.SetText("hello");
			Assert.Equal("hello", svc.GetText());
		}

		[Fact]
		public void SetText_NullArgument_Throws()
		{
			var svc = new TestableClipboardService();
			Assert.Throws<ArgumentNullException>(() => svc.SetText(null));
		}

		// ── Clear ──────────────────────────────────────────────────────────────

		[Fact]
		public void Clear_EmptiesClipboard()
		{
			var svc = new TestableClipboardService();
			svc.SetText("secret");
			svc.Clear();
			Assert.Null(svc.GetText());
			Assert.Equal(1, svc.ClearCallCount);
		}

		[Fact]
		public void Clear_ResetsOwnershipHash()
		{
			var svc = new TestableClipboardService();
			svc.SetText("secret");
			svc.Clear();

			// After Clear(), ClearIfOwner should be a no-op (hash is gone).
			svc.SimulateExternalWrite("injected");
			svc.ClearIfOwner();
			Assert.Equal("injected", svc.GetText()); // not cleared
		}

		// ── ClearIfOwner ───────────────────────────────────────────────────────

		[Fact]
		public void ClearIfOwner_WhenOwner_ClearsClipboard()
		{
			var svc = new TestableClipboardService();
			svc.SetText("secret");
			svc.ClearIfOwner();
			Assert.Null(svc.GetText());
			Assert.Equal(1, svc.ClearCallCount);
		}

		[Fact]
		public void ClearIfOwner_WhenNotOwner_DoesNotClear()
		{
			var svc = new TestableClipboardService();
			svc.SetText("secret");

			// Simulate another app replacing the clipboard text.
			svc.SimulateExternalWrite("from another app");
			svc.ClearIfOwner();

			// The text from the other app must be preserved.
			Assert.Equal("from another app", svc.GetText());
			Assert.Equal(0, svc.ClearCallCount);
		}

		[Fact]
		public void ClearIfOwner_WhenNeverSet_IsNoOp()
		{
			var svc = new TestableClipboardService();
			svc.ClearIfOwner(); // must not throw
			Assert.Equal(0, svc.ClearCallCount);
		}

		[Fact]
		public void ClearIfOwner_WhenClipboardIsNull_IsNoOp()
		{
			var svc = new TestableClipboardService();
			svc.SetText("secret");
			svc.SimulateExternalWrite(null); // simulate clipboard cleared externally
			svc.ClearIfOwner();
			Assert.Equal(0, svc.ClearCallCount);
		}

		// ── CopyText ───────────────────────────────────────────────────────────

		[Fact]
		public void CopyText_WithOwnership_AllowsClearIfOwner()
		{
			var svc = new TestableClipboardService();
			svc.CopyText("pass", setOwnership: true);
			svc.ClearIfOwner();
			Assert.Null(svc.GetText());
		}

		[Fact]
		public void CopyText_WithoutOwnership_PreventsClearIfOwner()
		{
			var svc = new TestableClipboardService();
			svc.CopyText("pass", setOwnership: false);
			svc.ClearIfOwner(); // must not clear because ownership was not claimed
			Assert.Equal("pass", svc.GetText());
		}

		[Fact]
		public void CopyText_NullArgument_Throws()
		{
			var svc = new TestableClipboardService();
			Assert.Throws<ArgumentNullException>(() =>
				svc.CopyText(null, setOwnership: true));
		}

		// ── SetWithAutoClear ───────────────────────────────────────────────────

		[Fact]
		public void SetWithAutoClear_NegativeTimeout_DoesNotStartTimer()
		{
			using var svc = new TestableClipboardService();
			svc.SetWithAutoClear("data", TimeSpan.FromSeconds(-1));
			Assert.False(svc.IsAutoClearActive);
		}

		// ── StartAutoClear / StopAutoClear ────────────────────────────────────

		[Fact]
		public void StartAutoClear_ZeroOrNegative_DoesNotStartTimer()
		{
			using var svc = new TestableClipboardService();
			svc.SetText("data");
			svc.StartAutoClear(0);
			Assert.False(svc.IsAutoClearActive);
		}

		[Fact]
		public void StartAutoClear_Positive_SetsIsAutoClearActive()
		{
			using var svc = new TestableClipboardService();
			svc.SetText("data");
			svc.StartAutoClear(60); // long enough it won't fire during the test
			Assert.True(svc.IsAutoClearActive);
			svc.StopAutoClear();
		}

		[Fact]
		public void StopAutoClear_CancelsTimer()
		{
			using var svc = new TestableClipboardService();
			svc.SetText("data");
			svc.StartAutoClear(60);
			Assert.True(svc.IsAutoClearActive);
			svc.StopAutoClear();
			Assert.False(svc.IsAutoClearActive);
		}

		[Fact]
		public void AutoClearTimer_FiresAfterInterval_ClearsClipboard()
		{
			using var svc = new TestableClipboardService();
			svc.SetText("secret");
			svc.StartAutoClear(1); // 1 second

			// Wait up to 2.5 seconds for the timer to fire.
			int waited = 0;
			while(svc.GetText() != null && waited < 250)
			{
				Thread.Sleep(10);
				waited++;
			}

			Assert.Null(svc.GetText()); // clipboard must have been cleared
			Assert.Equal(1, svc.ClearCallCount);
			Assert.False(svc.IsAutoClearActive);
		}

		[Fact]
		public void AutoClearTimer_ReplacedByNewStartAutoClear()
		{
			using var svc = new TestableClipboardService();
			svc.SetText("data");
			svc.StartAutoClear(60);
			svc.StartAutoClear(60); // second call replaces the first
			Assert.True(svc.IsAutoClearActive);
			svc.StopAutoClear();
			Assert.False(svc.IsAutoClearActive);
		}

		// ── IsAutoClearActive default ──────────────────────────────────────────

		[Fact]
		public void IsAutoClearActive_DefaultsToFalse()
		{
			using var svc = new TestableClipboardService();
			Assert.False(svc.IsAutoClearActive);
		}

		// ── Dispose ────────────────────────────────────────────────────────────

		[Fact]
		public void Dispose_StopsRunningTimer()
		{
			var svc = new TestableClipboardService();
			svc.SetText("data");
			svc.StartAutoClear(60);
			Assert.True(svc.IsAutoClearActive);

			svc.Dispose();
			Assert.False(svc.IsAutoClearActive);
		}

		[Fact]
		public void Dispose_WhenNoTimer_DoesNotThrow()
		{
			var svc = new TestableClipboardService();
			svc.Dispose(); // must not throw
		}
	}
}
