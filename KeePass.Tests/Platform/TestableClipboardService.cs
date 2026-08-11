/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using KeePass.Core.Platform;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// In-memory <see cref="ClipboardServiceBase"/> implementation for use in
	/// unit tests (WO-040).  Backed by a single string field — no OS clipboard
	/// APIs are invoked.
	///
	/// <para>Tests can read <see cref="Clipboard"/> directly to verify that
	/// <see cref="IClipboardService.Clear"/> or <see cref="IClipboardService.ClearIfOwner"/>
	/// emptied it without going through the clipboard API.</para>
	/// </summary>
	public sealed class TestableClipboardService : ClipboardServiceBase
	{
		/// <summary>Gets the current in-memory clipboard value.</summary>
		public string Clipboard { get; private set; }

		/// <summary>Tracks the number of times <see cref="DoClear"/> was invoked.</summary>
		public int ClearCallCount { get; private set; }

		/// <inheritdoc/>
		public override bool IsSupported => true;

		/// <inheritdoc/>
		protected override void DoCopyText(string text) { Clipboard = text; }

		/// <inheritdoc/>
		protected override string DoGetText() { return Clipboard; }

		/// <inheritdoc/>
		protected override void DoClear()
		{
			Clipboard = null;
			++ClearCallCount;
		}

		/// <summary>
		/// Simulates an external application writing to the clipboard without
		/// updating the ownership hash, so that <see cref="IClipboardService.ClearIfOwner"/>
		/// should NOT clear it.
		/// </summary>
		public void SimulateExternalWrite(string externalText)
		{
			Clipboard = externalText;
		}
	}
}
