/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

namespace KeePass.Core.Projections
{
	/// <summary>
	/// Immutable reference to a binary attachment on a
	/// <see cref="KeePassLib.PwEntry"/>.
	///
	/// <para>Avoids copying the full byte array into the projection;
	/// carries only the metadata needed to display the attachment list
	/// in the UI and to identify the content by hash.</para>
	/// </summary>
	public sealed class BinaryReference
	{
		/// <summary>The attachment name (e.g. <c>"document.pdf"</c>).</summary>
		public string Name { get; init; }

		/// <summary>The byte size of the binary content.</summary>
		public long Size { get; init; }

		/// <summary>
		/// SHA-256 hex digest of the unprotected binary content.
		/// Used by the UI to detect content changes between history snapshots.
		/// </summary>
		public string ContentHash { get; init; }

		public BinaryReference() { }
	}
}
