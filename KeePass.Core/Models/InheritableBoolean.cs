/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

namespace KeePass.Core.Models
{
	/// <summary>
	/// Three-state boolean used for group settings (e.g. auto-type, searching)
	/// that can either be enabled, disabled, or inherited from the parent group.
	/// </summary>
	public enum InheritableBoolean
	{
		/// <summary>Inherit the value from the parent group.</summary>
		Inherit = 0,

		/// <summary>Explicitly enabled, overriding the parent value.</summary>
		Enabled = 1,

		/// <summary>Explicitly disabled, overriding the parent value.</summary>
		Disabled = 2
	}

	/// <summary>
	/// Extension methods for converting between <see cref="InheritableBoolean"/>
	/// and the <c>bool?</c> representation used by <c>PwGroup</c>.
	/// </summary>
	public static class InheritableBooleanExtensions
	{
		/// <summary>
		/// Converts a nullable bool (as stored by <c>PwGroup</c>) to an
		/// <see cref="InheritableBoolean"/>.
		/// <c>null</c> → <see cref="InheritableBoolean.Inherit"/>,
		/// <c>true</c> → <see cref="InheritableBoolean.Enabled"/>,
		/// <c>false</c> → <see cref="InheritableBoolean.Disabled"/>.
		/// </summary>
		public static InheritableBoolean FromNullableBool(bool? value)
		{
			if (!value.HasValue) return InheritableBoolean.Inherit;
			return value.Value ? InheritableBoolean.Enabled : InheritableBoolean.Disabled;
		}

		/// <summary>
		/// Converts an <see cref="InheritableBoolean"/> to a nullable bool
		/// compatible with <c>PwGroup</c> properties.
		/// </summary>
		public static bool? ToNullableBool(this InheritableBoolean ib)
		{
			switch (ib)
			{
				case InheritableBoolean.Enabled:  return true;
				case InheritableBoolean.Disabled: return false;
				default:                          return null;
			}
		}
	}
}
