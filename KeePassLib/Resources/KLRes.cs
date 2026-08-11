/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace KeePassLib.Resources
{
	public static partial class KLRes
	{
		public static string FileSaveFailed
		{
			get { return KLRes.FileSaveFailed2; }
		}

		[Obsolete]
		public static string UserAccountKeyError
		{
			get { return KLRes.UnknownError; }
		}

		/// <summary>
		/// Error surfaced when the post-commit integrity check finds the vault
		/// file missing or empty after a transactional save.  Includes
		/// {0} = original file path.
		/// </summary>
		public static string VaultFileMissingAfterSave =>
			"The vault file is missing or empty after saving. " +
			"KeePass was unable to confirm the write succeeded.\r\n\r\n" +
			"File: {0}\r\n\r\n" +
			"Recovery: restore your vault from the most recent backup " +
			"(File \u2192 Open \u2192 Recent Files), or locate a backup " +
			"copy in the same directory with a .tmp extension.";

		/// <summary>
		/// Error surfaced when the post-commit integrity check detects a
		/// truncated or corrupted vault file (header signature mismatch).
		/// Includes {0} = original file path.
		/// </summary>
		public static string VaultFileCorruptAfterSave =>
			"The vault file appears corrupted after saving " +
			"(header signature mismatch). " +
			"The on-disk content may be incomplete.\r\n\r\n" +
			"File: {0}\r\n\r\n" +
			"Recovery: restore your vault from a backup before the last save, " +
			"or contact support with this message.";
	}
}
