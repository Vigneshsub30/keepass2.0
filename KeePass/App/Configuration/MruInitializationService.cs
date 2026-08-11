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

using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace KeePass.App.Configuration
{
	/// <summary>
	/// Performs post-load MRU deduplication and key-source deduplication for a
	/// freshly loaded <see cref="AppConfigEx"/>.
	///
	/// This logic was previously embedded in <see cref="AppConfigEx.OnLoad"/>,
	/// creating a layer violation: the configuration loading layer should not need
	/// to know that <c>MruList.AddItem</c> uses case-insensitive comparison for
	/// duplicate detection.  By moving the deduplication here, <c>OnLoad</c> is
	/// reduced to pure deserialization post-processing and the controller-layer
	/// knowledge is confined to this dedicated service.
	/// </summary>
	public static class MruInitializationService
	{
		/// <summary>
		/// Deduplicates the MRU file list and key-source associations stored in
		/// <paramref name="config"/> using the same case-insensitive comparison
		/// that <c>MruList.AddItem</c> employs, preventing duplicate entries when
		/// a regular config file and an enforced config file both reference the
		/// same path in different cases or relativity.
		///
		/// Must be called during application startup after <see cref="AppConfigSerializer.Load"/>
		/// and before the UI is displayed.
		/// </summary>
		/// <param name="config">The loaded configuration to deduplicate in place.</param>
		public static void Initialize(AppConfigEx config)
		{
			if(config == null) throw new ArgumentNullException("config");

			AceApplication aceApp = config.Application;
			AceDefaults aceDef = config.Defaults;

			// Deduplicate MRU items using the same CaseIgnoreCmp comparison that
			// MruList.AddItem uses, so the file list reflects what the user would
			// see if they had opened each file once.
			aceApp.MostRecentlyUsed.Items = new List<IOConnectionInfo>(
				MemUtil.Distinct<IOConnectionInfo, string>(
					aceApp.MostRecentlyUsed.Items,
					ioc => ioc.GetDisplayName().ToUpperInvariant(),
					true));

			// cf. AppConfigEx.GetNodeKey: key-source paths are matched
			// case-insensitively during enforced-config merges, so we must
			// deduplicate with the same comparison.
			aceDef.KeySources = new List<AceKeyAssoc>(
				MemUtil.Distinct<AceKeyAssoc, string>(
					aceDef.KeySources,
					a => a.DatabasePath.ToUpperInvariant(),
					true));
		}
	}
}
