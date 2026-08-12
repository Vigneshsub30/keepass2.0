/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System.Runtime.InteropServices;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Shared test constants and platform helpers for WO-044 characterization
	/// and regression tests.
	/// </summary>
	public static class TestFixtures
	{
		// ── Clipboard test constants ─────────────────────────────────────────

		/// <summary>Latin text used for clipboard round-trip tests.</summary>
		public const string ClipboardTestText = "KeePass-platform-test-2026";

		/// <summary>ASCII text without special chars for broad tool compatibility.</summary>
		public const string ClipboardSimpleText = "hello-keepass";

		/// <summary>Text with Unicode characters to verify encoding round-trips.</summary>
		public const string ClipboardUnicodeText = "Kee\u00dfast-\u4e2d\u6587-test";

		// ── Credential store test constants ──────────────────────────────────

		/// <summary>
		/// Key name used for credential store round-trip tests.
		/// Must be namespaced to avoid colliding with real KeePass credentials.
		/// </summary>
		public const string CredentialTestKey = "KeePass.Tests.Platform.WO044";

		/// <summary>
		/// Known byte array stored as a credential.  Fixed value so CI test
		/// output is deterministic.
		/// </summary>
		public static readonly byte[] CredentialTestSecret = new byte[]
		{
			0x57, 0x4F, 0x30, 0x34, // "WO04"
			0x34, 0x5F, 0x73, 0x65, // "4_se"
			0x63, 0x72, 0x65, 0x74  // "cret"
		};

		/// <summary>Hex representation of <see cref="CredentialTestSecret"/>.</summary>
		public const string CredentialTestSecretHex = "574f303434_73656372657474";

		// ── Platform detection expected values ───────────────────────────────

		/// <summary><c>true</c> when the test is running on Windows.</summary>
		public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		/// <summary><c>true</c> when the test is running on macOS.</summary>
		public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

		/// <summary><c>true</c> when the test is running on Linux.</summary>
		public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

		/// <summary><c>true</c> when the test is running on a Unix-like OS.</summary>
		public static bool IsUnix => IsMacOS || IsLinux;
	}
}
