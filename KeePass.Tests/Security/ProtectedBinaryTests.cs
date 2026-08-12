using System;
using System.Linq;
using System.Text;
using KeePassLib.Security;
using Xunit;

namespace KeePass.Tests.Security
{
    /// <summary>
    /// Characterization tests for ProtectedBinary — the in-memory encrypted
    /// binary type used to hold sensitive data such as file attachments.
    ///
    /// Tests cover: construction, ReadData round-trip, IsProtected, Length,
    /// equality comparison, and edge cases (empty data, large data).
    ///
    /// Cross-platform: no Windows-specific APIs.
    /// </summary>
    public class ProtectedBinaryTests
    {
        // ── 1. Construction and ReadData ─────────────────────────────────────

        [Fact]
        public void ProtectedBinary_Protected_ReadDataReturnsOriginal()
        {
            byte[] data = Encoding.UTF8.GetBytes("SecretContent");
            var pb = new ProtectedBinary(true, data);
            Assert.Equal(data, pb.ReadData());
        }

        [Fact]
        public void ProtectedBinary_Unprotected_ReadDataReturnsOriginal()
        {
            byte[] data = { 0x01, 0x02, 0x03, 0xFF };
            var pb = new ProtectedBinary(false, data);
            Assert.Equal(data, pb.ReadData());
        }

        [Fact]
        public void ProtectedBinary_EmptyData_ReadDataReturnsEmptyArray()
        {
            var pb = new ProtectedBinary(true, Array.Empty<byte>());
            Assert.Empty(pb.ReadData());
        }

        // ── 2. IsProtected ───────────────────────────────────────────────────

        [Fact]
        public void ProtectedBinary_IsProtected_TrueWhenProtected()
        {
            var pb = new ProtectedBinary(true, new byte[] { 1, 2 });
            Assert.True(pb.IsProtected);
        }

        [Fact]
        public void ProtectedBinary_IsProtected_FalseWhenUnprotected()
        {
            var pb = new ProtectedBinary(false, new byte[] { 1, 2 });
            Assert.False(pb.IsProtected);
        }

        // ── 3. Length ────────────────────────────────────────────────────────

        [Fact]
        public void ProtectedBinary_Length_MatchesByteArrayLength()
        {
            byte[] data = { 10, 20, 30, 40, 50 };
            var pb = new ProtectedBinary(true, data);
            Assert.Equal((uint)data.Length, pb.Length);
        }

        [Fact]
        public void ProtectedBinary_EmptyData_LengthIsZero()
        {
            var pb = new ProtectedBinary(false, Array.Empty<byte>());
            Assert.Equal(0U, pb.Length);
        }

        // ── 4. Equality ──────────────────────────────────────────────────────

        [Fact]
        public void ProtectedBinary_Equals_SameContent_ReturnsTrue()
        {
            byte[] data = { 0xAB, 0xCD, 0xEF };
            var a = new ProtectedBinary(true,  data);
            var b = new ProtectedBinary(false, data);
            // bCheckProtEqual=false: only compare byte content
            Assert.True(a.Equals(b, false));
        }

        [Fact]
        public void ProtectedBinary_Equals_DifferentContent_ReturnsFalse()
        {
            var a = new ProtectedBinary(true, new byte[] { 1, 2, 3 });
            var b = new ProtectedBinary(true, new byte[] { 4, 5, 6 });
            Assert.False(a.Equals(b, false));
        }

        [Fact]
        public void ProtectedBinary_Equals_SameContentDifferentProtection_CheckBothFalse()
        {
            byte[] data = { 0x42, 0x43 };
            var a = new ProtectedBinary(true,  data);
            var b = new ProtectedBinary(false, data);
            // bCheckProtEqual=true: protection state must also match
            Assert.False(a.Equals(b, true));
        }

        [Fact]
        public void ProtectedBinary_Equals_SameContentSameProtection_CheckBothTrue()
        {
            byte[] data = { 0x01 };
            var a = new ProtectedBinary(true, data);
            var b = new ProtectedBinary(true, data);
            Assert.True(a.Equals(b, true));
        }

        // ── 5. Edge cases ────────────────────────────────────────────────────

        [Fact]
        public void ProtectedBinary_LargeData_ReadDataPreservesAllBytes()
        {
            // 10 KB of deterministic data
            byte[] data = Enumerable.Range(0, 10 * 1024)
                                    .Select(i => (byte)(i % 256))
                                    .ToArray();
            var pb = new ProtectedBinary(true, data);
            byte[] read = pb.ReadData();

            Assert.Equal((uint)data.Length, pb.Length);
            Assert.Equal(data, read);
        }

        [Fact]
        public void ProtectedBinary_MultipleReads_ReturnSameData()
        {
            byte[] data = Encoding.UTF8.GetBytes("repeat-me");
            var pb = new ProtectedBinary(true, data);

            byte[] first  = pb.ReadData();
            byte[] second = pb.ReadData();
            Assert.Equal(first, second);
        }
    }
}
