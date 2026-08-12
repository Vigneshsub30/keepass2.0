#nullable enable

using System;
using System.Text.RegularExpressions;

using KeePassLib.Utility;

using Xunit;

namespace KeePass.Tests.AutoType
{
	/// <summary>
	/// Tests for the window-title matching logic used by AutoType.IsMatchWindow.
	/// The pure logic (pattern matching and regex) is tested via
	/// <see cref="StrUtil.SimplePatternMatch"/> and <see cref="Regex.IsMatch"/>,
	/// which are the two branches inside IsMatchWindow.
	/// </summary>
	public sealed class WindowMatchingTests
	{
		// ── Helpers ───────────────────────────────────────────────────── //

		/// <summary>
		/// Mirrors AutoType.IsMatchWindow logic without calling Program.Config.
		/// </summary>
		private static bool IsMatchWindow(string strWindow, string strFilter,
			bool normDashes = false)
		{
			if(strWindow == null) return false;
			if(strFilter == null) return false;

			string strF = NormWindowText(strFilter, normDashes);
			string strW = NormWindowText(strWindow, normDashes);

			int ccF = strF.Length;
			if((ccF > 4) && (strF[0] == '/') && (strF[1] == '/') &&
				(strF[ccF - 2] == '/') && (strF[ccF - 1] == '/'))
			{
				try
				{
					string strRx = strF.Substring(2, ccF - 4);
					return Regex.IsMatch(strW, strRx, RegexOptions.IgnoreCase);
				}
				catch(Exception) { return false; }
			}

			return StrUtil.SimplePatternMatch(strF, strW, StrUtil.CaseIgnoreCmp);
		}

		private static readonly char[] s_normToHyphen =
		{
			'\u2010', '\u2011', '\u2012', '\u2013',
			'\u2014', '\u2015', '\u2212'
		};

		private static string NormWindowText(string str, bool normDashes)
		{
			if(string.IsNullOrEmpty(str)) return string.Empty;
			str = str.Trim();
			if(normDashes)
				foreach(char c in s_normToHyphen)
					str = str.Replace(c, '-');
			return str;
		}

		// ── Exact match ───────────────────────────────────────────────── //

		[Fact]
		public void ExactMatch_SameString_ReturnsTrue()
			=> Assert.True(IsMatchWindow("Notepad", "Notepad"));

		[Fact]
		public void ExactMatch_CaseInsensitive_ReturnsTrue()
			=> Assert.True(IsMatchWindow("notepad", "NOTEPAD"));

		[Fact]
		public void ExactMatch_Different_ReturnsFalse()
			=> Assert.False(IsMatchWindow("Notepad", "Word"));

		// ── Wildcard: prefix (STRING*) ─────────────────────────────────── //

		[Fact]
		public void WildcardPrefix_Matches()
			=> Assert.True(IsMatchWindow("Notepad - C:\\file.txt", "Notepad*"));

		[Fact]
		public void WildcardPrefix_DoesNotMatchWhenPrefixWrong()
			=> Assert.False(IsMatchWindow("Word - doc.docx", "Notepad*"));

		// ── Wildcard: suffix (*STRING) ─────────────────────────────────── //

		[Fact]
		public void WildcardSuffix_Matches()
			=> Assert.True(IsMatchWindow("KeePass - vault.kdbx", "*.kdbx"));

		[Fact]
		public void WildcardSuffix_DoesNotMatchWhenSuffixWrong()
			=> Assert.False(IsMatchWindow("KeePass - vault.kdbx", "*.docx"));

		// ── Wildcard: contains (*STRING*) ─────────────────────────────── //

		[Fact]
		public void WildcardContains_Matches()
			=> Assert.True(IsMatchWindow("My KeePass Window", "*KeePass*"));

		[Fact]
		public void WildcardContains_DoesNotMatchWhenAbsent()
			=> Assert.False(IsMatchWindow("My Word Window", "*KeePass*"));

		// ── Double wildcard (**) ───────────────────────────────────────── //

		[Fact]
		public void DoubleWildcard_MatchesAnything()
			=> Assert.True(IsMatchWindow("anything at all", "**"));

		// ── Wildcard: middle part (prefix*middle*suffix) ──────────────── //

		[Fact]
		public void WildcardMiddle_Matches()
			=> Assert.True(IsMatchWindow("Start - Content - End", "Start*Content*End"));

		[Fact]
		public void WildcardMiddle_DoesNotMatch()
			=> Assert.False(IsMatchWindow("Start - End", "Start*Content*End"));

		// ── Regex patterns (//pattern//) ──────────────────────────────── //

		[Fact]
		public void RegexPattern_Matches()
			=> Assert.True(IsMatchWindow("Firefox - My Site", "//firefox.+site//"));

		[Fact]
		public void RegexPattern_CaseInsensitive()
			=> Assert.True(IsMatchWindow("NOTEPAD", "//notepad//"));

		[Fact]
		public void RegexPattern_NoMatch()
			=> Assert.False(IsMatchWindow("Chrome - Google", "//firefox.+//"));

		[Fact]
		public void RegexPattern_InvalidRegex_ReturnsFalse()
			=> Assert.False(IsMatchWindow("Anything", "//[invalid(//"));

		// ── Edge cases ────────────────────────────────────────────────── //

		[Fact]
		public void EmptyWindow_EmptyFilter_NoMatch()
			=> Assert.False(IsMatchWindow(string.Empty, string.Empty));

		[Fact]
		public void WhitespaceWindow_Trimmed_EmptyWindow_NoMatch()
			=> Assert.False(IsMatchWindow("   ", "   "));

		[Fact]
		public void NullWindow_ReturnsFalse()
			=> Assert.False(IsMatchWindow(null!, "anything"));

		[Fact]
		public void NullFilter_ReturnsFalse()
			=> Assert.False(IsMatchWindow("anything", null!));

		// ── Unicode ───────────────────────────────────────────────────── //

		[Fact]
		public void UnicodeWindow_ExactMatch()
			=> Assert.True(IsMatchWindow("паролевый менеджер", "паролевый менеджер"));

		[Fact]
		public void UnicodeWindow_WildcardMatch()
			=> Assert.True(IsMatchWindow("КееПасс — сейф", "*КееПасс*"));

		// ── Dash normalisation ─────────────────────────────────────────── //

		[Fact]
		public void NormDashes_EmDashInWindowMatchesHyphenFilter()
			=> Assert.True(IsMatchWindow(
				"KeePass \u2014 vault.kdbx",
				"KeePass - vault.kdbx",
				normDashes: true));

		[Fact]
		public void NormDashes_Disabled_EmDashDoesNotMatchHyphenFilter()
			=> Assert.False(IsMatchWindow(
				"KeePass \u2014 vault.kdbx",
				"KeePass - vault.kdbx",
				normDashes: false));
	}
}
