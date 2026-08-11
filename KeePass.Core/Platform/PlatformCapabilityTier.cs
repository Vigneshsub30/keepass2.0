/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

namespace KeePass.Core.Platform
{
	/// <summary>
	/// Describes the degree to which a <see cref="PlatformCapability"/> is
	/// available on the current platform.
	/// </summary>
	public enum PlatformCapabilityTier
	{
		/// <summary>
		/// The capability is fully available and behaves as expected on all
		/// supported configurations of this platform.
		/// </summary>
		Full,

		/// <summary>
		/// The capability is available but with restrictions.  Callers should
		/// verify specific sub-conditions before relying on it (e.g. Wayland
		/// clipboard supports write but not read on some compositors).
		/// </summary>
		Partial,

		/// <summary>
		/// The capability is not available on this platform.  Callers must
		/// provide a graceful fallback.
		/// </summary>
		Unsupported,
	}
}
