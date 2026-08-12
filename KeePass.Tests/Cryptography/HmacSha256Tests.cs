using System.Security.Cryptography;
using KeePassLib.Utility;
using Xunit;

namespace KeePass.Tests.Cryptography
{
    /// <summary>
    /// xUnit promotion of the HMAC-SHA-256 self-test vectors from SelfTest.TestHmac().
    /// Test vectors from RFC 4231.
    /// </summary>
    public class HmacSha256Tests
    {
        private static readonly byte[] s_empty = new byte[0];

        private static byte[] ComputeHmac(byte[] pbKey, byte[] pbMsg)
        {
            using (HMACSHA256 h = new HMACSHA256(pbKey))
            {
                h.TransformBlock(pbMsg, 0, pbMsg.Length, pbMsg, 0);
                h.TransformFinalBlock(s_empty, 0, 0);
                return h.Hash;
            }
        }

        private static byte[] ComputeHmacReuse(byte[] pbKey, byte[] pbMsg)
        {
            // Exercises the Initialize() / re-use path
            using (HMACSHA256 h = new HMACSHA256(pbKey))
            {
                h.TransformBlock(pbMsg, 0, pbMsg.Length, pbMsg, 0);
                h.TransformFinalBlock(s_empty, 0, 0);
                byte[] ignored = h.Hash;

                h.Initialize();
                h.TransformBlock(pbMsg, 0, pbMsg.Length, pbMsg, 0);
                h.TransformFinalBlock(s_empty, 0, 0);
                return h.Hash;
            }
        }

        // ── RFC 4231 test vector 1 ───────────────────────────────────────────
        [Fact]
        public void HmacSha256_Rfc4231_V1_HiThere()
        {
            byte[] pbKey = new byte[20];
            for (int i = 0; i < pbKey.Length; ++i) pbKey[i] = 0x0B;
            byte[] pbMsg = StrUtil.Utf8.GetBytes("Hi There");

            byte[] pbExpected = new byte[32]
            {
                0xB0, 0x34, 0x4C, 0x61, 0xD8, 0xDB, 0x38, 0x53,
                0x5C, 0xA8, 0xAF, 0xCE, 0xAF, 0x0B, 0xF1, 0x2B,
                0x88, 0x1D, 0xC2, 0x00, 0xC9, 0x83, 0x3D, 0xA7,
                0x26, 0xE9, 0x37, 0x6C, 0x2E, 0x32, 0xCF, 0xF7
            };

            Assert.Equal(pbExpected, ComputeHmac(pbKey, pbMsg));
        }

        // Verify that the same result is produced on object re-use (Initialize path)
        [Fact]
        public void HmacSha256_Rfc4231_V1_Reuse_ProducesSameResult()
        {
            byte[] pbKey = new byte[20];
            for (int i = 0; i < pbKey.Length; ++i) pbKey[i] = 0x0B;
            byte[] pbMsg = StrUtil.Utf8.GetBytes("Hi There");

            byte[] pbExpected = new byte[32]
            {
                0xB0, 0x34, 0x4C, 0x61, 0xD8, 0xDB, 0x38, 0x53,
                0x5C, 0xA8, 0xAF, 0xCE, 0xAF, 0x0B, 0xF1, 0x2B,
                0x88, 0x1D, 0xC2, 0x00, 0xC9, 0x83, 0x3D, 0xA7,
                0x26, 0xE9, 0x37, 0x6C, 0x2E, 0x32, 0xCF, 0xF7
            };

            Assert.Equal(pbExpected, ComputeHmacReuse(pbKey, pbMsg));
        }

        // ── RFC 4231 test vector 7 ───────────────────────────────────────────
        // Key and data both larger than HMAC block size (131-byte key)
        [Fact]
        public void HmacSha256_Rfc4231_V7_LargeKeyAndData()
        {
            byte[] pbKey = new byte[131];
            for (int i = 0; i < pbKey.Length; ++i) pbKey[i] = 0xAA;
            byte[] pbMsg = StrUtil.Utf8.GetBytes(
                "This is a test using a larger than block-size key and " +
                "a larger than block-size data. The key needs to be " +
                "hashed before being used by the HMAC algorithm.");

            byte[] pbExpected = new byte[32]
            {
                0x9B, 0x09, 0xFF, 0xA7, 0x1B, 0x94, 0x2F, 0xCB,
                0x27, 0x63, 0x5F, 0xBC, 0xD5, 0xB0, 0xE9, 0x44,
                0xBF, 0xDC, 0x63, 0x64, 0x4F, 0x07, 0x13, 0x93,
                0x8A, 0x7F, 0x51, 0x53, 0x5C, 0x3A, 0x35, 0xE2
            };

            Assert.Equal(pbExpected, ComputeHmac(pbKey, pbMsg));
        }
    }
}
