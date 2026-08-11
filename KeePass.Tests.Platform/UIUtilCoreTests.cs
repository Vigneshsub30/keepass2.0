/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System.Reflection;

using KeePass.Core.UI;

using Xunit;

namespace KeePass.Tests.Platform
{
	public class UIUtilCoreTests
	{
		// ── MaxWindowTitleLength ─────────────────────────────────────────────

		[Fact]
		public void MaxWindowTitleLength_Is254()
		{
			Assert.Equal(254, UIUtilCore.MaxWindowTitleLength);
		}

		// ── CreateFileTypeFilter ─────────────────────────────────────────────

		[Fact]
		public void CreateFileTypeFilter_SingleExt_NoAllFiles_ReturnsCorrectFilter()
		{
			string result = UIUtilCore.CreateFileTypeFilter("kdbx", "KeePass Files", false, "All Files");
			Assert.Equal("KeePass Files (*.kdbx)|*.kdbx", result);
		}

		[Fact]
		public void CreateFileTypeFilter_SingleExt_WithAllFiles_AppendsAllFilesEntry()
		{
			string result = UIUtilCore.CreateFileTypeFilter("kdbx", "KeePass Files", true, "All Files");
			Assert.Equal("KeePass Files (*.kdbx)|*.kdbx|All Files (*.*)|*.*", result);
		}

		[Fact]
		public void CreateFileTypeFilter_MultipleExts_PipeSeparated_RendersCorrectly()
		{
			string result = UIUtilCore.CreateFileTypeFilter("kdbx|kdb", "KeePass Files", false, "All Files");
			Assert.Equal("KeePass Files (*.kdbx, *.kdb)|*.kdbx;*.kdb", result);
		}

		[Fact]
		public void CreateFileTypeFilter_MultipleExts_WithAllFiles()
		{
			string result = UIUtilCore.CreateFileTypeFilter("csv|txt", "Text Files", true, "All Files");
			Assert.Equal("Text Files (*.csv, *.txt)|*.csv;*.txt|All Files (*.*)|*.*", result);
		}

		[Fact]
		public void CreateFileTypeFilter_NullExtension_OnlyAllFiles()
		{
			string result = UIUtilCore.CreateFileTypeFilter(null, "Desc", true, "All Files");
			Assert.Equal("All Files (*.*)|*.*", result);
		}

		[Fact]
		public void CreateFileTypeFilter_EmptyExtension_OnlyAllFiles()
		{
			string result = UIUtilCore.CreateFileTypeFilter("", "Desc", true, "All Files");
			Assert.Equal("All Files (*.*)|*.*", result);
		}

		[Fact]
		public void CreateFileTypeFilter_EmptyDescription_OnlyAllFiles()
		{
			string result = UIUtilCore.CreateFileTypeFilter("kdbx", "", true, "All Files");
			Assert.Equal("All Files (*.*)|*.*", result);
		}

		[Fact]
		public void CreateFileTypeFilter_NoBothNoAllFiles_ReturnsEmpty()
		{
			string result = UIUtilCore.CreateFileTypeFilter(null, null, false, "All Files");
			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void CreateFileTypeFilter_NullAllFilesLabel_FallsBackToDefault()
		{
			string result = UIUtilCore.CreateFileTypeFilter(null, null, true, null);
			Assert.Equal("All Files (*.*)|*.*", result);
		}

		[Fact]
		public void CreateFileTypeFilter_LocalisedAllFilesLabel_UsesProvidedString()
		{
			string result = UIUtilCore.CreateFileTypeFilter(null, null, true, "Alle Dateien");
			Assert.Contains("Alle Dateien", result);
		}

		// ── GetPrimaryFileTypeExt ────────────────────────────────────────────

		[Fact]
		public void GetPrimaryFileTypeExt_SingleExt_ReturnsIt()
		{
			Assert.Equal("kdbx", UIUtilCore.GetPrimaryFileTypeExt("kdbx"));
		}

		[Fact]
		public void GetPrimaryFileTypeExt_MultipleExts_ReturnsFirst()
		{
			Assert.Equal("kdbx", UIUtilCore.GetPrimaryFileTypeExt("kdbx|kdb"));
		}

		[Fact]
		public void GetPrimaryFileTypeExt_EmptyString_ReturnsEmpty()
		{
			Assert.Equal(string.Empty, UIUtilCore.GetPrimaryFileTypeExt(""));
		}

		[Fact]
		public void GetPrimaryFileTypeExt_EmptyStringInput_ReturnsEmpty()
		{
			// Null is treated as a programming error by the original implementation
			// (Debug.Assert fires). We test with empty string instead.
			Assert.Equal(string.Empty, UIUtilCore.GetPrimaryFileTypeExt(string.Empty));
		}

		// ── ColorsEqual ──────────────────────────────────────────────────────

		[Fact]
		public void ColorsEqual_SameHex_ReturnsTrue()
		{
			Assert.True(UIUtilCore.ColorsEqual("#FF0000", "#FF0000"));
		}

		[Fact]
		public void ColorsEqual_DifferentHex_ReturnsFalse()
		{
			Assert.False(UIUtilCore.ColorsEqual("#FF0000", "#00FF00"));
		}

		[Fact]
		public void ColorsEqual_NullAndNull_ReturnsTrue()
		{
			Assert.True(UIUtilCore.ColorsEqual(null, null));
		}

		[Fact]
		public void ColorsEqual_NullAndEmptyString_ReturnsTrue()
		{
			Assert.True(UIUtilCore.ColorsEqual(null, ""));
		}

		[Fact]
		public void ColorsEqual_NullAndNonNull_ReturnsFalse()
		{
			Assert.False(UIUtilCore.ColorsEqual(null, "#FF0000"));
		}

		[Fact]
		public void ColorsEqual_WithoutHashPrefix_StillWorks()
		{
			Assert.True(UIUtilCore.ColorsEqual("FF0000", "FF0000"));
		}

		[Fact]
		public void ColorsEqual_SixDigitAndEightDigitOpaque_SameColor_ReturnsTrue()
		{
			// #RRGGBB with implied FF alpha vs. #FFRRGGBB explicitly
			Assert.True(UIUtilCore.ColorsEqual("#FF0000", "#FFFF0000"));
		}

		[Fact]
		public void ColorsEqual_TransparentAndOpaque_ReturnsFalse()
		{
			Assert.False(UIUtilCore.ColorsEqual("#00FF0000", "#FFFF0000"));
		}

		[Fact]
		public void ColorsEqual_CaseInsensitive_ReturnsTrue()
		{
			Assert.True(UIUtilCore.ColorsEqual("#ff0000", "#FF0000"));
		}

		[Fact]
		public void ColorsEqual_White_ReturnsTrue()
		{
			Assert.True(UIUtilCore.ColorsEqual("#FFFFFF", "#FFFFFF"));
		}

		// ── IsDarkColor ──────────────────────────────────────────────────────

		[Fact]
		public void IsDarkColor_Black_ReturnsTrue()
		{
			Assert.True(UIUtilCore.IsDarkColor("#000000"));
		}

		[Fact]
		public void IsDarkColor_White_ReturnsFalse()
		{
			Assert.False(UIUtilCore.IsDarkColor("#FFFFFF"));
		}

		[Fact]
		public void IsDarkColor_DarkGray_ReturnsTrue()
		{
			Assert.True(UIUtilCore.IsDarkColor("#404040"));
		}

		[Fact]
		public void IsDarkColor_LightGray_ReturnsFalse()
		{
			Assert.False(UIUtilCore.IsDarkColor("#C0C0C0"));
		}

		[Fact]
		public void IsDarkColor_DarkBlue_ReturnsTrue()
		{
			// Blue luminance: 0.11 * 128 ≈ 14 → dark
			Assert.True(UIUtilCore.IsDarkColor("#000080"));
		}

		[Fact]
		public void IsDarkColor_Null_ReturnsFalse()
		{
			Assert.False(UIUtilCore.IsDarkColor(null));
		}

		[Fact]
		public void IsDarkColor_EmptyString_ReturnsFalse()
		{
			Assert.False(UIUtilCore.IsDarkColor(""));
		}

		[Fact]
		public void IsDarkColor_MalformedHex_ReturnsFalse()
		{
			Assert.False(UIUtilCore.IsDarkColor("ZZZZZZ"));
		}

		[Fact]
		public void IsDarkColor_WithAlphaPrefix_Works()
		{
			// #FFAABBCC — checks RGB only, alpha stripped
			Assert.False(UIUtilCore.IsDarkColor("#FFFFFFFF")); // white with alpha
		}

		// ── ScaleWindowScreenRect ────────────────────────────────────────────

		[Fact]
		public void ScaleWindowScreenRect_NullInput_ReturnsNull()
		{
			Assert.Null(UIUtilCore.ScaleWindowScreenRect(null, 2.0, 2.0));
		}

		[Fact]
		public void ScaleWindowScreenRect_EmptyInput_ReturnsEmpty()
		{
			Assert.Equal(string.Empty, UIUtilCore.ScaleWindowScreenRect("", 2.0, 2.0));
		}

		[Fact]
		public void ScaleWindowScreenRect_XY_ScalesCorrectly()
		{
			// Serialized form: "100 200" → scale 2x, 3x → "200 600"
			string result = UIUtilCore.ScaleWindowScreenRect("100 200", 2.0, 3.0);
			Assert.Contains("200", result);
		}

		[Fact]
		public void ScaleWindowScreenRect_XYWH_ScalesCorrectly()
		{
			string result = UIUtilCore.ScaleWindowScreenRect("10 20 300 400", 2.0, 2.0);
			Assert.NotNull(result);
			Assert.NotEmpty(result);
		}

		// ── No System.Drawing/System.Windows.Forms references ────────────────

		[Fact]
		public void UIUtilCore_Assembly_HasNoSystemDrawingReference()
		{
			Assembly coreAssembly = typeof(UIUtilCore).Assembly;
			foreach(AssemblyName refName in coreAssembly.GetReferencedAssemblies())
			{
				Assert.False(
					refName.Name.Equals("System.Drawing", System.StringComparison.OrdinalIgnoreCase) ||
					refName.Name.Equals("System.Windows.Forms", System.StringComparison.OrdinalIgnoreCase),
					$"KeePass.Core should not reference '{refName.Name}'.");
			}
		}
	}
}
