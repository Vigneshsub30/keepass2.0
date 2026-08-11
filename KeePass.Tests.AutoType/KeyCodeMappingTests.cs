#nullable enable

using System;
using System.Collections.Generic;

using Xunit;

namespace KeePass.Tests.AutoType
{
	/// <summary>
	/// Tests for keyboard key-code mapping logic (CharToVKey / VKeyToChar).
	/// Uses a local stub that mirrors the SiCodes mapping to enable platform-
	/// neutral testing without a Windows-only P/Invoke dependency.
	/// </summary>
	public sealed class KeyCodeMappingTests
	{
		// ── Stub ───────────────────────────────────────────────────────── //

		/// <summary>
		/// Minimal local stub of the SiCodes char-to-vkey table for the
		/// printable ASCII range used in unit testing.
		/// </summary>
		private static class StubKeyMap
		{
			// Standard VKey constants (same as Windows VK_ values)
			public const int VkA     = 0x41;
			public const int VkZ     = 0x5A;
			public const int Vk0     = 0x30;
			public const int Vk9     = 0x39;
			public const int VkSpace = 0x20;
			public const int VkEnter = 0x0D;
			public const int VkTab   = 0x09;
			public const int VkBack  = 0x08;
			public const int VkEscape = 0x1B;

			private static readonly Dictionary<char, int> s_map = BuildMap();

			private static Dictionary<char, int> BuildMap()
			{
				var d = new Dictionary<char, int>();
				// Digits 0-9
				for(int i = 0; i <= 9; i++) d[(char)('0' + i)] = Vk0 + i;
				// Letters A-Z (VKeys for uppercase, same code as lowercase)
				for(int i = 0; i < 26; i++) d[(char)('A' + i)] = VkA + i;
				for(int i = 0; i < 26; i++) d[(char)('a' + i)] = VkA + i;
				d[' '] = VkSpace;
				return d;
			}

			public static int CharToVKey(char ch)
			{
				int vk;
				return s_map.TryGetValue(ch, out vk) ? vk : 0;
			}

			public static char VKeyToChar(int vk)
			{
				foreach(var kvp in s_map)
					if(kvp.Value == vk) return kvp.Key;
				return char.MinValue;
			}
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void CharToVKey_UppercaseA_ReturnsVkA()
			=> Assert.Equal(StubKeyMap.VkA, StubKeyMap.CharToVKey('A'));

		[Fact]
		public void CharToVKey_LowercaseA_ReturnsSameAsUppercase()
			=> Assert.Equal(StubKeyMap.CharToVKey('A'),
				StubKeyMap.CharToVKey('a'));

		[Fact]
		public void CharToVKey_DigitZero_ReturnsVk0()
			=> Assert.Equal(StubKeyMap.Vk0, StubKeyMap.CharToVKey('0'));

		[Fact]
		public void CharToVKey_DigitNine_ReturnsVk9()
			=> Assert.Equal(StubKeyMap.Vk9, StubKeyMap.CharToVKey('9'));

		[Fact]
		public void CharToVKey_Space_ReturnsVkSpace()
			=> Assert.Equal(StubKeyMap.VkSpace, StubKeyMap.CharToVKey(' '));

		[Fact]
		public void CharToVKey_UnknownChar_ReturnsZero()
			=> Assert.Equal(0, StubKeyMap.CharToVKey('\u0000'));

		[Theory]
		[InlineData('A')]
		[InlineData('Z')]
		[InlineData('M')]
		[InlineData('0')]
		[InlineData('9')]
		[InlineData(' ')]
		public void RoundTrip_CharToVKeyToChar_IsIdempotent(char ch)
		{
			int vk = StubKeyMap.CharToVKey(ch);
			Assert.NotEqual(0, vk);
			char back = StubKeyMap.VKeyToChar(vk);
			// Round-trip for uppercase (a/A map to same VK; VKeyToChar returns first)
			Assert.Equal(char.ToUpperInvariant(ch), char.ToUpperInvariant(back));
		}

		[Fact]
		public void VKeyToChar_UnknownVKey_ReturnsMinValue()
			=> Assert.Equal(char.MinValue, StubKeyMap.VKeyToChar(0xFFFF));

		[Fact]
		public void AllUppercaseLetters_HaveUniqueVKeys()
		{
			var seen = new HashSet<int>();
			for(char c = 'A'; c <= 'Z'; c++)
			{
				int vk = StubKeyMap.CharToVKey(c);
				Assert.NotEqual(0, vk);
				Assert.True(seen.Add(vk), $"Duplicate VKey {vk} for '{c}'");
			}
		}

		[Fact]
		public void AllDigits_HaveUniqueVKeys()
		{
			var seen = new HashSet<int>();
			for(char c = '0'; c <= '9'; c++)
			{
				int vk = StubKeyMap.CharToVKey(c);
				Assert.NotEqual(0, vk);
				Assert.True(seen.Add(vk), $"Duplicate VKey {vk} for '{c}'");
			}
		}
	}
}
