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
	/// Identifies a discrete platform-dependent capability that application
	/// code may query at runtime via
	/// <see cref="IPlatformIntegration.GetCapabilityTier"/>.
	///
	/// <para>New capabilities should be added here before the corresponding
	/// platform implementation is authored; all implementations default to
	/// <see cref="PlatformCapabilityTier.Unsupported"/> for unknown values so
	/// older builds remain compatible.</para>
	/// </summary>
	public enum PlatformCapability
	{
		/// <summary>
		/// Read/write clipboard access.
		/// Full = programmatic clipboard reads are allowed;
		/// Partial = write-only (e.g. some Wayland compositors without
		/// wlr-data-control); Unsupported = clipboard APIs not available.
		/// </summary>
		Clipboard,

		/// <summary>
		/// Ability to set a content-type / privacy marker on clipboard data
		/// so that clipboard managers exclude sensitive entries from their
		/// history.  Supported on some Wayland compositors; not available on
		/// Windows or X11.
		/// </summary>
		ClipboardPrivacyMarkers,

		/// <summary>
		/// Persistent OS-native credential storage (Windows Credential Manager,
		/// macOS Keychain, libsecret on Linux).
		/// </summary>
		CredentialStore,

		/// <summary>
		/// Keyboard auto-type injection into the foreground application.
		/// Windows-only in the current implementation.
		/// </summary>
		AutoType,

		/// <summary>
		/// Secure Desktop (UIPI-elevated desktop) for showing password prompts
		/// in isolation from malicious window hooks.  Windows-only.
		/// </summary>
		SecureDesktop,

		/// <summary>
		/// Preventing the KeePass window contents from appearing in OS-level
		/// screen captures or remote desktop sessions.
		/// Full = all content protected; Partial = window-level protection only.
		/// Windows and macOS; unavailable on Linux.
		/// </summary>
		ScreenCaptureProtection,

		/// <summary>
		/// Setting a mandatory integrity label / DACL on the KeePass process to
		/// prevent lower-integrity processes from injecting into it.
		/// Windows-only.
		/// </summary>
		ProcessDacl,

		/// <summary>
		/// System-wide hotkey registration to summon KeePass or trigger
		/// auto-type from any foreground window.
		/// </summary>
		GlobalHotKeys,
	}
}
