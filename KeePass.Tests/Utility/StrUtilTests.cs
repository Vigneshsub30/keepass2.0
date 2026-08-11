using System;
using System.Text;
using KeePassLib.Utility;
using Xunit;

namespace KeePass.Tests.Utility
{
    /// <summary>
    /// Characterization tests for StrUtil — string utilities used throughout
    /// KeePassLib: encoding, natural comparison, bool conversion, and URL helpers.
    ///
    /// Cross-platform: no Windows-specific APIs.
    /// </summary>
    public class StrUtilTests
    {
        // ── 1. StrUtil.Utf8 encoding round-trip ─────────────────────────────

        [Fact]
        public void Utf8_AsciiString_RoundTrips()
        {
            const string s = "Hello, KeePass!";
            byte[] encoded = StrUtil.Utf8.GetBytes(s);
            string decoded = StrUtil.Utf8.GetString(encoded);
            Assert.Equal(s, decoded);
        }

        [Fact]
        public void Utf8_CjkString_RoundTrips()
        {
            const string cjk = "密码管理器🔐";
            byte[] encoded = StrUtil.Utf8.GetBytes(cjk);
            string decoded = StrUtil.Utf8.GetString(encoded);
            Assert.Equal(cjk, decoded);
        }

        [Fact]
        public void Utf8_EmptyString_RoundTrips()
        {
            byte[] encoded = StrUtil.Utf8.GetBytes(string.Empty);
            string decoded = StrUtil.Utf8.GetString(encoded);
            Assert.Equal(string.Empty, decoded);
        }

        [Fact]
        public void Utf8_Encoding_HasNoBom()
        {
            // StrUtil.Utf8 must not write a BOM (critical for KDBX XML serialization)
            byte[] preamble = StrUtil.Utf8.GetPreamble();
            Assert.Empty(preamble);
        }

        // ── 2. CompareNaturally ──────────────────────────────────────────────

        [Fact]
        public void CompareNaturally_NumericSegments_OrdersCorrectly()
        {
            // "item2" should sort before "item10" in natural order
            int result = StrUtil.CompareNaturally("item2", "item10");
            Assert.True(result < 0,
                "'item2' should come before 'item10' in natural sort");
        }

        [Fact]
        public void CompareNaturally_EqualStrings_ReturnsZero()
        {
            Assert.Equal(0, StrUtil.CompareNaturally("abc", "abc"));
        }

        [Fact]
        public void CompareNaturally_PureText_AlphabeticalOrder()
        {
            int result = StrUtil.CompareNaturally("alpha", "beta");
            Assert.True(result < 0,
                "'alpha' should come before 'beta' alphabetically");
        }

        [Fact]
        public void CompareNaturally_NumberPrefix_OrdersNumerically()
        {
            // "2 files" before "10 files"
            int result = StrUtil.CompareNaturally("2 files", "10 files");
            Assert.True(result < 0,
                "'2 files' should come before '10 files' in natural sort");
        }

        // ── 3. BoolToString / StringToBool ──────────────────────────────────

        [Theory]
        [InlineData(true,  "true")]
        [InlineData(false, "false")]
        public void BoolToString_ProducesExpectedOutput(bool input, string expected)
        {
            Assert.Equal(expected, StrUtil.BoolToString(input));
        }

        [Theory]
        [InlineData("true",     true)]
        [InlineData("True",     true)]
        [InlineData("TRUE",     true)]
        [InlineData("yes",      true)]
        [InlineData("1",        true)]
        [InlineData("enabled",  true)]
        [InlineData("false",    false)]
        [InlineData("False",    false)]
        [InlineData("no",       false)]
        [InlineData("0",        false)]
        [InlineData("disabled", false)]
        public void StringToBool_ParsesKnownValues(string input, bool expected)
        {
            Assert.Equal(expected, StrUtil.StringToBool(input));
        }

        [Fact]
        public void StringToBool_NullOrEmpty_ReturnsFalse()
        {
            Assert.False(StrUtil.StringToBool(null));
            Assert.False(StrUtil.StringToBool(string.Empty));
        }

        [Fact]
        public void BoolToString_StringToBool_RoundTrip()
        {
            Assert.True(StrUtil.StringToBool(StrUtil.BoolToString(true)));
            Assert.False(StrUtil.StringToBool(StrUtil.BoolToString(false)));
        }

        // ── 4. StringToBoolEx (nullable) ─────────────────────────────────────

        [Fact]
        public void StringToBoolEx_UnrecognizedValue_ReturnsNull()
        {
            Assert.Null(StrUtil.StringToBoolEx("maybe"));
            Assert.Null(StrUtil.StringToBoolEx(null));
            Assert.Null(StrUtil.StringToBoolEx(string.Empty));
        }

        [Fact]
        public void StringToBoolEx_KnownValues_ReturnsNonNull()
        {
            Assert.True(StrUtil.StringToBoolEx("true") ?? false);
            Assert.False(StrUtil.StringToBoolEx("false") ?? true);
        }
    }
}
