using System.Text;
using KeePassLib.Security;
using Xunit;

namespace KeePass.Tests.Security
{
    /// <summary>
    /// Characterization tests for ProtectedString — the in-memory encrypted
    /// string type used to hold sensitive data such as passwords.
    ///
    /// Tests cover: construction, ReadString round-trip, IsProtected flag,
    /// IsEmpty, Length, equality comparison, WithProtection toggling, and
    /// Unicode content preservation.
    ///
    /// Cross-platform: no Windows-specific APIs.
    /// </summary>
    public class ProtectedStringTests
    {
        // ── 1. Construction and ReadString ───────────────────────────────────

        [Fact]
        public void ProtectedString_Protected_ReadStringReturnsOriginal()
        {
            var ps = new ProtectedString(true, "MySuperSecret");
            Assert.Equal("MySuperSecret", ps.ReadString());
        }

        [Fact]
        public void ProtectedString_Unprotected_ReadStringReturnsOriginal()
        {
            var ps = new ProtectedString(false, "PlainText");
            Assert.Equal("PlainText", ps.ReadString());
        }

        [Fact]
        public void ProtectedString_Empty_ReadStringReturnsEmpty()
        {
            var ps = new ProtectedString(true, string.Empty);
            Assert.Equal(string.Empty, ps.ReadString());
        }

        // ── 2. IsProtected ───────────────────────────────────────────────────

        [Fact]
        public void ProtectedString_IsProtected_TrueWhenProtected()
        {
            var ps = new ProtectedString(true, "secret");
            Assert.True(ps.IsProtected);
        }

        [Fact]
        public void ProtectedString_IsProtected_FalseWhenUnprotected()
        {
            var ps = new ProtectedString(false, "visible");
            Assert.False(ps.IsProtected);
        }

        // ── 3. IsEmpty and Length ────────────────────────────────────────────

        [Fact]
        public void ProtectedString_IsEmpty_TrueForEmptyString()
        {
            var ps = new ProtectedString(false, string.Empty);
            Assert.True(ps.IsEmpty);
        }

        [Fact]
        public void ProtectedString_IsEmpty_FalseForNonEmpty()
        {
            var ps = new ProtectedString(true, "nonempty");
            Assert.False(ps.IsEmpty);
        }

        [Fact]
        public void ProtectedString_Length_MatchesStringLength()
        {
            const string value = "KeePass!";
            var ps = new ProtectedString(true, value);
            Assert.Equal(value.Length, ps.Length);
        }

        // ── 4. Equality ──────────────────────────────────────────────────────

        [Fact]
        public void ProtectedString_Equals_SameContent_ReturnsTrue()
        {
            var a = new ProtectedString(true,  "password");
            var b = new ProtectedString(false, "password");
            // bCheckProtEqual=false: only compare string value, not protection state
            Assert.True(a.Equals(b, false));
        }

        [Fact]
        public void ProtectedString_Equals_DifferentContent_ReturnsFalse()
        {
            var a = new ProtectedString(true, "abc");
            var b = new ProtectedString(true, "xyz");
            Assert.False(a.Equals(b, false));
        }

        [Fact]
        public void ProtectedString_Equals_SameContentDifferentProtection_CheckBothFalse()
        {
            var a = new ProtectedString(true,  "same");
            var b = new ProtectedString(false, "same");
            // bCheckProtEqual=true: protection state must also match → not equal
            Assert.False(a.Equals(b, true));
        }

        [Fact]
        public void ProtectedString_Equals_SameContentSameProtection_CheckBothTrue()
        {
            var a = new ProtectedString(true, "same");
            var b = new ProtectedString(true, "same");
            Assert.True(a.Equals(b, true));
        }

        // ── 5. WithProtection ────────────────────────────────────────────────

        [Fact]
        public void ProtectedString_WithProtection_TogglesProtectionState()
        {
            var original = new ProtectedString(false, "changeMe");
            var toggled  = original.WithProtection(true);

            Assert.False(original.IsProtected);
            Assert.True(toggled.IsProtected);
        }

        [Fact]
        public void ProtectedString_WithProtection_PreservesContent()
        {
            var original = new ProtectedString(false, "SensitiveData");
            var protected_ = original.WithProtection(true);

            Assert.Equal("SensitiveData", protected_.ReadString());
        }

        // ── 6. Unicode content ───────────────────────────────────────────────

        [Fact]
        public void ProtectedString_Unicode_EmojiRoundTrips()
        {
            const string emoji = "🔐🗝️🔑";
            var ps = new ProtectedString(true, emoji);
            Assert.Equal(emoji, ps.ReadString());
        }

        [Fact]
        public void ProtectedString_Unicode_CjkRoundTrips()
        {
            const string cjk = "密码안전비밀暗号";
            var ps = new ProtectedString(true, cjk);
            Assert.Equal(cjk, ps.ReadString());
        }
    }
}
