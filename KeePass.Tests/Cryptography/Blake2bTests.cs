using System.Text;
using KeePassLib.Cryptography.Hash;
using KeePassLib.Utility;
using Xunit;

namespace KeePass.Tests.Cryptography
{
    /// <summary>
    /// xUnit promotion of the Blake2b self-test vectors from SelfTest.TestBlake2b().
    /// RFC 7693 vector and official b2sum reference outputs.
    /// </summary>
    public class Blake2bTests
    {
        // ── RFC 7693 ─────────────────────────────────────────────────────────
        [Fact]
        public void Blake2b_Rfc7693_AsciiAbc()
        {
            byte[] pbData = StrUtil.Utf8.GetBytes("abc");
            byte[] pbExpected = new byte[64]
            {
                0xBA, 0x80, 0xA5, 0x3F, 0x98, 0x1C, 0x4D, 0x0D,
                0x6A, 0x27, 0x97, 0xB6, 0x9F, 0x12, 0xF6, 0xE9,
                0x4C, 0x21, 0x2F, 0x14, 0x68, 0x5A, 0xC4, 0xB7,
                0x4B, 0x12, 0xBB, 0x6F, 0xDB, 0xFF, 0xA2, 0xD1,
                0x7D, 0x87, 0xC5, 0x39, 0x2A, 0xAB, 0x79, 0x2D,
                0xC2, 0x52, 0xD5, 0xDE, 0x45, 0x33, 0xCC, 0x95,
                0x18, 0xD3, 0x8A, 0xA8, 0xDB, 0xF1, 0x92, 0x5A,
                0xB9, 0x23, 0x86, 0xED, 0xD4, 0x00, 0x99, 0x23
            };

            Blake2b h = new Blake2b();
            byte[] pbResult = h.ComputeHash(pbData);

            Assert.Equal(pbExpected, pbResult);
        }

        private static readonly byte[] s_empty = new byte[0];

        // ── Official b2sum tool: empty input ─────────────────────────────────
        [Fact]
        public void Blake2b_B2Sum_EmptyInput()
        {
            byte[] pbExpected = new byte[64]
            {
                0x78, 0x6A, 0x02, 0xF7, 0x42, 0x01, 0x59, 0x03,
                0xC6, 0xC6, 0xFD, 0x85, 0x25, 0x52, 0xD2, 0x72,
                0x91, 0x2F, 0x47, 0x40, 0xE1, 0x58, 0x47, 0x61,
                0x8A, 0x86, 0xE2, 0x17, 0xF7, 0x1F, 0x54, 0x19,
                0xD2, 0x5E, 0x10, 0x31, 0xAF, 0xEE, 0x58, 0x53,
                0x13, 0x89, 0x64, 0x44, 0x93, 0x4E, 0xB0, 0x4B,
                0x90, 0x3A, 0x68, 0x5B, 0x14, 0x48, 0xB7, 0x55,
                0xD5, 0x6F, 0x70, 0x1A, 0xFE, 0x9B, 0xE2, 0xCE
            };

            Blake2b h = new Blake2b();
            byte[] pbResult = h.ComputeHash(s_empty);

            Assert.Equal(pbExpected, pbResult);
        }

        // ── Official b2sum tool: 1000-repetition uppercase string ────────────
        [Fact]
        public void Blake2b_B2Sum_LongAlphanumericString()
        {
            const string strS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.:,;_-\r\n";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 1000; ++i) sb.Append(strS);
            byte[] pbData = StrUtil.Utf8.GetBytes(sb.ToString());

            byte[] pbExpected = new byte[64]
            {
                0x59, 0x69, 0x8D, 0x3B, 0x83, 0xF4, 0x02, 0x4E,
                0xD8, 0x99, 0x26, 0x0E, 0xF4, 0xE5, 0x9F, 0x20,
                0xDC, 0x31, 0xEE, 0x5B, 0x45, 0xEA, 0xBB, 0xFC,
                0x1C, 0x0A, 0x8E, 0xED, 0xAA, 0x7A, 0xFF, 0x50,
                0x82, 0xA5, 0x8F, 0xBC, 0x4A, 0x46, 0xFC, 0xC5,
                0xEF, 0x44, 0x4E, 0x89, 0x80, 0x7D, 0x3F, 0x1C,
                0xC1, 0x94, 0x45, 0xBB, 0xC0, 0x2C, 0x95, 0xAA,
                0x3F, 0x08, 0x8A, 0x93, 0xF8, 0x75, 0x91, 0xB0
            };

            Blake2b h = new Blake2b();

            // Use the streaming TransformBlock / TransformFinalBlock API
            // to exercise the incremental hashing path
            int p = 0;
            while (p < pbData.Length)
            {
                // Vary chunk size to exercise partial-block handling
                int cb = System.Math.Min(31, pbData.Length - p);
                h.TransformBlock(pbData, p, cb, pbData, p);
                p += cb;
            }
            h.TransformFinalBlock(s_empty, 0, 0);

            Assert.Equal(pbExpected, h.Hash);

            h.Clear();
        }
    }
}
