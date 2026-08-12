/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using KeePassLib.Utility;

namespace KeePass.Core.UI
{
	/// <summary>
	/// Platform-neutral UI utility helpers.
	///
	/// <para>This class must not reference <c>System.Drawing</c>,
	/// <c>System.Windows.Forms</c>, or any other platform-specific assembly.
	/// It contains the subset of the original <c>UIUtil</c> static class that
	/// can be shared across UI heads (WinForms, Avalonia, etc.).</para>
	/// </summary>
	public static class UIUtilCore
	{
		// ── Constants ─────────────────────────────────────────────────────────

		/// <summary>
		/// Maximum number of characters allowed in a window title bar string.
		/// Matches the Windows API limit.
		/// </summary>
		public const int MaxWindowTitleLength = 254;

		// ── File-type filter helpers ──────────────────────────────────────────

		/// <summary>
		/// Builds a file-dialog type filter string of the form
		/// <c>"Description (*.ext)|*.ext|All Files (*.*)|*.*"</c>.
		///
		/// <para>
		/// <paramref name="strExtension"/> may contain multiple pipe-separated
		/// extensions (e.g. <c>"kdbx|kdb"</c>).
		/// When <paramref name="bIncludeAllFiles"/> is <c>true</c>, an "All Files"
		/// entry is appended; <paramref name="strAllFilesLabel"/> is used as its
		/// display label (typically the localised string from the application's
		/// resource table).
		/// </para>
		/// </summary>
		public static string CreateFileTypeFilter(string strExtension,
			string strDescription, bool bIncludeAllFiles, string strAllFilesLabel)
		{
			StringBuilder sb = new StringBuilder();

			if(!string.IsNullOrEmpty(strExtension) && !string.IsNullOrEmpty(strDescription))
			{
				string[] vExts = strExtension.Split(new char[] { '|' },
					StringSplitOptions.RemoveEmptyEntries);
				if(vExts.Length > 0)
				{
					sb.Append(strDescription);
					sb.Append(@" (*.");

					for(int i = 0; i < vExts.Length; ++i)
					{
						if(i > 0) sb.Append(@", *.");
						sb.Append(vExts[i]);
					}

					sb.Append(@")|*.");

					for(int i = 0; i < vExts.Length; ++i)
					{
						if(i > 0) sb.Append(@";*.");
						sb.Append(vExts[i]);
					}
				}
			}

			if(bIncludeAllFiles)
			{
				if(sb.Length > 0) sb.Append('|');
				sb.Append(strAllFilesLabel ?? "All Files");
				sb.Append(@" (*.*)|*.*");
			}

			return sb.ToString();
		}

		/// <summary>
		/// Returns the primary (first) extension from a pipe-separated
		/// extension string (e.g. returns <c>"kdbx"</c> from <c>"kdbx|kdb"</c>).
		/// </summary>
		public static string GetPrimaryFileTypeExt(string strExtensions)
		{
			if(strExtensions == null) { Debug.Assert(false); return string.Empty; }

			int i = strExtensions.IndexOf('|');
			if(i >= 0) return strExtensions.Substring(0, i);

			return strExtensions;
		}

		// ── Color helpers (hex string representation) ─────────────────────────

		/// <summary>
		/// Compares two ARGB hex colour strings for equality.
		///
		/// <para>Accepts <c>#RRGGBB</c> (fully opaque assumed) and
		/// <c>#AARRGGBB</c> formats.  The comparison is case-insensitive.
		/// <c>null</c> and empty strings are treated as transparent
		/// (ARGB 0x00000000).</para>
		/// </summary>
		public static bool ColorsEqual(string hexColor1, string hexColor2)
		{
			return ParseArgb(hexColor1) == ParseArgb(hexColor2);
		}

		/// <summary>
		/// Returns <c>true</c> when the colour described by <paramref name="hexColor"/>
		/// is perceptually dark (grayscale luminance &lt; 128).
		///
		/// <para>Uses standard luminance weights: 0.30 R + 0.59 G + 0.11 B.
		/// Returns <c>false</c> for <c>null</c>, empty, or unparseable inputs.</para>
		/// </summary>
		public static bool IsDarkColor(string hexColor)
		{
			byte r, g, b;
			if(!TryParseRgb(hexColor, out r, out g, out b)) return false;

			int luminance = (int)(0.3f * r + 0.59f * g + 0.11f * b);
			return (luminance < 128);
		}

		// ── Window rect helpers ───────────────────────────────────────────────

		/// <summary>
		/// Scales a serialised window-screen rectangle string by the given
		/// horizontal and vertical factors.
		///
		/// <para>The rect string is the format produced by
		/// <see cref="KeePassLib.Utility.StrUtil.SerializeIntArray"/> and contains
		/// at least X and Y coordinates, optionally followed by Width and Height.
		/// Returns the original string unchanged when scaling fails.</para>
		/// </summary>
		public static string ScaleWindowScreenRect(string strRect, double sX, double sY)
		{
			if(string.IsNullOrEmpty(strRect)) return strRect;

			try
			{
				string str = strRect.Replace(",", string.Empty); // Backward compat.

				int[] v = StrUtil.DeserializeIntArray(str);
				if((v == null) || (v.Length < 2)) { Debug.Assert(false); return strRect; }

				v[0] = (int)Math.Round((double)v[0] * sX); // X
				v[1] = (int)Math.Round((double)v[1] * sY); // Y

				if(v.Length >= 4)
				{
					v[2] = (int)Math.Round((double)v[2] * sX); // Width
					v[3] = (int)Math.Round((double)v[3] * sY); // Height
				}

				return StrUtil.SerializeIntArray(v);
			}
			catch(Exception) { Debug.Assert(false); }

			return strRect;
		}

		// ── Private colour parsing helpers ────────────────────────────────────

		/// <summary>
		/// Parses a hex colour string to a 32-bit ARGB value.
		/// Returns 0 for null, empty, or malformed input.
		/// </summary>
		private static uint ParseArgb(string hex)
		{
			if(string.IsNullOrEmpty(hex)) return 0u;

			if(hex[0] == '#') hex = hex.Substring(1);

			if(hex.Length == 6) hex = "FF" + hex; // assume fully opaque

			if(hex.Length != 8) return 0u;

			uint result;
			if(!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
				return 0u;

			return result;
		}

		/// <summary>
		/// Parses the R, G, B components from a hex colour string.
		/// Returns <c>false</c> when parsing fails.
		/// </summary>
		private static bool TryParseRgb(string hex, out byte r, out byte g, out byte b)
		{
			r = 0; g = 0; b = 0;
			if(string.IsNullOrEmpty(hex)) return false;

			if(hex[0] == '#') hex = hex.Substring(1);

			// Strip alpha prefix if #AARRGGBB
			if(hex.Length == 8) hex = hex.Substring(2);

			if(hex.Length != 6) return false;

			uint rgb;
			if(!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
				return false;

			r = (byte)((rgb >> 16) & 0xFF);
			g = (byte)((rgb >> 8)  & 0xFF);
			b = (byte)(rgb & 0xFF);
			return true;
		}
	}
}
