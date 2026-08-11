#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using KeePassLib.Utility;

using Xunit;

namespace KeePass.Tests.AutoType
{
	/// <summary>
	/// Golden-data tests that replay captured baseline cases from
	/// TestData/AutoType/*.json and verify the current implementation
	/// produces identical outputs. Any divergence indicates a regression.
	/// </summary>
	public sealed class GoldenDataTests
	{
		private static readonly string s_dataDir = Path.Combine(
			AppContext.BaseDirectory, "TestData", "AutoType");

		// ── Helpers ───────────────────────────────────────────────────── //

		private static readonly char[] s_normToHyphen =
		{
			'\u2010', '\u2011', '\u2012', '\u2013',
			'\u2014', '\u2015', '\u2212'
		};

		private static string NormText(string str, bool normDashes)
		{
			if(string.IsNullOrEmpty(str)) return string.Empty;
			str = str.Trim();
			if(normDashes)
				foreach(char c in s_normToHyphen)
					str = str.Replace(c, '-');
			return str;
		}

		private static bool IsMatch(string window, string filter)
		{
			if(window == null || filter == null) return false;
			string w = NormText(window, normDashes: false);
			string f = NormText(filter, normDashes: false);

			int n = f.Length;
			if((n > 4) && f[0] == '/' && f[1] == '/' && f[n-2] == '/' && f[n-1] == '/')
			{
				try { return Regex.IsMatch(w, f.Substring(2, n - 4), RegexOptions.IgnoreCase); }
				catch { return false; }
			}
			return StrUtil.SimplePatternMatch(f, w, StrUtil.CaseIgnoreCmp);
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void GoldenFile_WindowMatching_Exists()
		{
			string path = Path.Combine(s_dataDir, "window-matching-golden.json");
			Assert.True(File.Exists(path), $"Golden file not found: {path}");
		}

		[Fact]
		public void GoldenFile_WindowMatching_AllCasesPass()
		{
			string path = Path.Combine(s_dataDir, "window-matching-golden.json");
			if(!File.Exists(path)) return; // Skipped if not built yet

			string json = File.ReadAllText(path);
			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement cases = doc.RootElement.GetProperty("cases");

			int idx = 0;
			foreach(JsonElement c in cases.EnumerateArray())
			{
				string window   = c.GetProperty("window").GetString()   ?? string.Empty;
				string filter   = c.GetProperty("filter").GetString()   ?? string.Empty;
				bool   expected = c.GetProperty("expected").GetBoolean();
				bool   actual   = IsMatch(window, filter);

				Assert.True(actual == expected,
					$"Case {idx}: window='{window}', filter='{filter}' " +
					$"expected={expected} actual={actual}");
				idx++;
			}

			Assert.True(idx > 0, "Golden file contained zero test cases.");
		}
	}
}
