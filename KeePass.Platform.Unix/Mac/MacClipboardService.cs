/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using KeePass.Core.Platform;
using KeePass.Platform.Unix.Shared;

namespace KeePass.Platform.Unix.Mac
{
	/// <summary>
	/// macOS implementation of <see cref="IClipboardService"/>, backed by the
	/// <c>pbcopy</c> (write) and <c>pbpaste</c> (read) CLI tools that ship
	/// with every macOS installation.
	///
	/// <para>Extends <see cref="ClipboardServiceBase"/> to inherit the
	/// SHA-256 ownership hash and auto-clear timer logic, delegating only the
	/// three platform-specific clipboard primitives.</para>
	///
	/// <para>NSPasteboard change-count is not accessible without ObjC interop;
	/// the base class SHA-256 hash is used as the ownership signal instead.</para>
	/// </summary>
	public sealed class MacClipboardService : ClipboardServiceBase
	{
		/// <inheritdoc/>
		public override bool IsSupported => true;

		/// <inheritdoc/>
		protected override void DoCopyText(string text)
		{
			// pbcopy reads its input from stdin and places it on the general pasteboard.
			ProcessRunner.RunSilent("pbcopy", string.Empty, stdinData: text);
		}

		/// <inheritdoc/>
		protected override string DoGetText()
		{
			// pbpaste outputs the current pasteboard content to stdout.
			return ProcessRunner.Run("pbpaste", string.Empty) ?? string.Empty;
		}

		/// <inheritdoc/>
		protected override void DoClear()
		{
			// Pipe an empty string to pbcopy to clear the pasteboard.
			ProcessRunner.RunSilent("pbcopy", string.Empty, stdinData: string.Empty);
		}
	}
}
