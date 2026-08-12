using System;
using System.Linq;
using KeePassLib.Utility;
using Xunit;

namespace KeePass.Tests.Utility
{
    /// <summary>
    /// Characterization tests for MemUtil — memory operations (array comparison,
    /// zeroing, compression, and integer serialization) used throughout KeePassLib.
    ///
    /// Cross-platform: no Windows-specific APIs.
    /// </summary>
    public class MemUtilTests
    {
        // ── 1. ArraysEqual ───────────────────────────────────────────────────

        [Fact]
        public void ArraysEqual_SameContent_ReturnsTrue()
        {
            byte[] a = { 0x01, 0x02, 0x03 };
            byte[] b = { 0x01, 0x02, 0x03 };
            Assert.True(MemUtil.ArraysEqual(a, b));
        }

        [Fact]
        public void ArraysEqual_DifferentContent_ReturnsFalse()
        {
            byte[] a = { 0x01, 0x02, 0x03 };
            byte[] b = { 0x01, 0x02, 0xFF };
            Assert.False(MemUtil.ArraysEqual(a, b));
        }

        [Fact]
        public void ArraysEqual_DifferentLengths_ReturnsFalse()
        {
            byte[] a = { 0x01, 0x02 };
            byte[] b = { 0x01, 0x02, 0x03 };
            Assert.False(MemUtil.ArraysEqual(a, b));
        }

        [Fact]
        public void ArraysEqual_BothEmpty_ReturnsTrue()
        {
            Assert.True(MemUtil.ArraysEqual(Array.Empty<byte>(), Array.Empty<byte>()));
        }

        [Fact]
        public void ArraysEqual_BothNull_ReturnsTrue()
        {
            Assert.True(MemUtil.ArraysEqual(null, null));
        }

        [Fact]
        public void ArraysEqual_OneNull_ReturnsFalse()
        {
            Assert.False(MemUtil.ArraysEqual(new byte[] { 1 }, null));
            Assert.False(MemUtil.ArraysEqual(null, new byte[] { 1 }));
        }

        // ── 2. ZeroByteArray ────────────────────────────────────────────────

        [Fact]
        public void ZeroByteArray_AllBytesBecome0()
        {
            byte[] data = { 0xAA, 0xBB, 0xCC, 0xDD };
            MemUtil.ZeroByteArray(data);
            Assert.All(data, b => Assert.Equal(0, b));
        }

        [Fact]
        public void ZeroByteArray_EmptyArray_DoesNotThrow()
        {
            byte[] empty = Array.Empty<byte>();
            MemUtil.ZeroByteArray(empty);  // should not throw
        }

        // ── 3. Compress / Decompress round-trip ──────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(10000)]
        public void CompressDecompress_RoundTrip_PreservesData(int byteCount)
        {
            byte[] original = Enumerable.Range(0, byteCount)
                                        .Select(i => (byte)(i % 251))
                                        .ToArray();
            byte[] compressed   = MemUtil.Compress(original);
            byte[] decompressed = MemUtil.Decompress(compressed);

            Assert.Equal(original, decompressed);
        }

        [Fact]
        public void CompressDecompress_HighlyCompressibleData_CompressedIsShorter()
        {
            // All-zero data compresses extremely well
            byte[] original = new byte[10000];
            byte[] compressed = MemUtil.Compress(original);
            Assert.True(compressed.Length < original.Length,
                "Highly compressible data should produce smaller compressed output");
        }

        // ── 4. Int32 bytes round-trip ────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [InlineData(0x12345678)]
        public void Int32ToBytes_BytesToInt32_RoundTrip(int value)
        {
            byte[] bytes = MemUtil.Int32ToBytes(value);
            int restored = MemUtil.BytesToInt32(bytes);
            Assert.Equal(value, restored);
        }

        // ── 5. Int64 bytes round-trip ────────────────────────────────────────

        [Theory]
        [InlineData(0L)]
        [InlineData(1L)]
        [InlineData(-1L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        [InlineData(0x123456789ABCDEF0L)]
        public void Int64ToBytes_BytesToInt64_RoundTrip(long value)
        {
            byte[] bytes = MemUtil.Int64ToBytes(value);
            long restored = MemUtil.BytesToInt64(bytes);
            Assert.Equal(value, restored);
        }

        // ── 6. UInt32 bytes round-trip ───────────────────────────────────────

        [Theory]
        [InlineData(0U)]
        [InlineData(1U)]
        [InlineData(uint.MaxValue)]
        [InlineData(0xDEADBEEFU)]
        public void UInt32ToBytes_BytesToUInt32_RoundTrip(uint value)
        {
            byte[] bytes = MemUtil.UInt32ToBytes(value);
            uint restored = MemUtil.BytesToUInt32(bytes);
            Assert.Equal(value, restored);
        }
    }
}
