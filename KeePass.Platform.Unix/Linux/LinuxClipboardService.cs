/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;

using KeePass.Core.Platform;
using KeePass.Platform.Unix.Shared;

namespace KeePass.Platform.Unix.Linux
{
	/// <summary>
	/// Linux implementation of <see cref="IClipboardService"/>, backed by
	/// whichever clipboard CLI tool is available on the running session.
	///
	/// <para>Tool selection order (cached after first detection):</para>
	/// <list type="bullet">
	///   <item>Wayland: <c>wl-copy</c>/<c>wl-paste</c> (wl-clipboard)</item>
	///   <item>X11 primary: <c>xsel --clipboard</c></item>
	///   <item>X11 fallback: <c>xclip -selection clipboard</c></item>
	/// </list>
	///
	/// <para>Extends <see cref="ClipboardServiceBase"/> to inherit SHA-256
	/// ownership-hash tracking and the auto-clear timer.</para>
	///
	/// <para>On KDE desktops, <see cref="DoClear"/> issues an additional
	/// <c>qdbus</c> call to flush the Klipper clipboard history, preventing
	/// a race condition where the previous clipboard content remains visible
	/// in the Klipper applet after a clear (workaround for KeePass issue #1613).</para>
	/// </summary>
	public sealed class LinuxClipboardService : ClipboardServiceBase
	{
		private enum ClipBackend { Unknown, Wayland, Xsel, Xclip }

		private ClipBackend _backend = ClipBackend.Unknown;
		private readonly object _backendLock = new object();

		/// <inheritdoc/>
		public override bool IsSupported => DetectBackend() != ClipBackend.Unknown;

		/// <inheritdoc/>
		protected override void DoCopyText(string text)
		{
			switch(RequireBackend())
			{
				case ClipBackend.Wayland:
					ProcessRunner.RunSilent("wl-copy", string.Empty, stdinData: text);
					break;
				case ClipBackend.Xsel:
					ProcessRunner.RunSilent("xsel", "--clipboard --input", stdinData: text);
					break;
				default: // Xclip
					ProcessRunner.RunSilent("xclip", "-selection clipboard", stdinData: text);
					break;
			}
		}

		/// <inheritdoc/>
		protected override string DoGetText()
		{
			ClipBackend be = DetectBackend();
			if(be == ClipBackend.Unknown) return string.Empty;

			string result;
			switch(be)
			{
				case ClipBackend.Wayland:
					result = ProcessRunner.Run("wl-paste", "--no-newline");
					break;
				case ClipBackend.Xsel:
					result = ProcessRunner.Run("xsel", "--clipboard --output");
					break;
				default: // Xclip
					result = ProcessRunner.Run("xclip", "-selection clipboard -o");
					break;
			}
			return result ?? string.Empty;
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Also issues a D-Bus <c>qdbus</c> command to clear the Klipper history
		/// when running on a KDE desktop, preventing the previous content from
		/// remaining visible in the applet after the clipboard is cleared.
		/// </remarks>
		protected override void DoClear()
		{
			ClipBackend be = DetectBackend();
			if(be == ClipBackend.Unknown) return;

			switch(be)
			{
				case ClipBackend.Wayland:
					ProcessRunner.RunSilent("wl-copy", "--clear");
					break;
				case ClipBackend.Xsel:
					ProcessRunner.RunSilent("xsel", "--clipboard --clear");
					break;
				default: // Xclip
					ProcessRunner.RunSilent("xclip", "-selection clipboard",
						stdinData: string.Empty);
					break;
			}

			ClearKlipperHistoryIfKde();
		}

		// ── KDE/Klipper workaround ─────────────────────────────────────────

		/// <summary>
		/// Sends a D-Bus message to Klipper to clear its clipboard history.
		/// Only called when the current desktop is KDE (<c>XDG_CURRENT_DESKTOP=KDE</c>
		/// or <c>DESKTOP_SESSION=kde*</c>) and <c>qdbus</c> is on PATH.
		/// This is a best-effort call; failure is silently ignored.
		/// </summary>
		private static void ClearKlipperHistoryIfKde()
		{
			if(!IsKdeDesktop()) return;

			// Klipper D-Bus interface: clearClipboardHistory resets the history
			// list, preventing the previous entry from remaining visible.
			ProcessRunner.RunSilent("qdbus",
				"org.kde.klipper /klipper " +
				"org.kde.klipper.klipper.clearClipboardHistory");
		}

		private static bool IsKdeDesktop()
		{
			string xdg = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
				?? string.Empty).Trim();
			if(xdg.Equals("KDE", StringComparison.OrdinalIgnoreCase)) return true;

			string ds = (Environment.GetEnvironmentVariable("DESKTOP_SESSION")
				?? string.Empty).Trim();
			return ds.StartsWith("kde", StringComparison.OrdinalIgnoreCase);
		}

		// ── Backend detection ─────────────────────────────────────────────

		private ClipBackend DetectBackend()
		{
			lock(_backendLock)
			{
				if(_backend != ClipBackend.Unknown) return _backend;

				// Prefer Wayland when the session is Wayland-native.
				string waylandDisplay =
					Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? string.Empty;
				if(waylandDisplay.Length > 0 &&
					ProcessRunner.Run("which", "wl-copy") != null)
				{
					_backend = ClipBackend.Wayland;
					return _backend;
				}

			// X11 backends require an active display session.  Without DISPLAY
			// set (e.g. in a headless CI runner) xclip and xsel silently fail
			// to connect; pretend they are absent so IsSupported returns false.
			string x11Display =
				Environment.GetEnvironmentVariable("DISPLAY") ?? string.Empty;
			if(x11Display.Length == 0)
				return ClipBackend.Unknown;

			// xsel is primary for X11; xclip is the fallback.
			if(ProcessRunner.Run("which", "xsel") != null)
			{
				_backend = ClipBackend.Xsel;
				return _backend;
			}

			if(ProcessRunner.Run("which", "xclip") != null)
			{
				_backend = ClipBackend.Xclip;
				return _backend;
			}

			// Leave as Unknown — IsSupported returns false.
			return ClipBackend.Unknown;
			}
		}

		private ClipBackend RequireBackend()
		{
			ClipBackend be = DetectBackend();
			if(be == ClipBackend.Unknown)
				throw new PlatformNotSupportedException(
					"No clipboard helper found. " +
					"Install wl-clipboard (Wayland), xsel, or xclip.");
			return be;
		}
	}
}
