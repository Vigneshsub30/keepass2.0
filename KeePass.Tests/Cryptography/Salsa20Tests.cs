using System;
using System.Collections.Generic;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Utility;
using Xunit;

namespace KeePass.Tests.Cryptography
{
    /// <summary>
    /// xUnit promotion of the Salsa20 self-test vectors from SelfTest.TestSalsa20().
    /// Test values from official set 6, vector 3.
    /// </summary>
    public class Salsa20Tests
    {
        private static readonly byte[] s_key = new byte[32]
        {
            0x0F, 0x62, 0xB5, 0x08, 0x5B, 0xAE, 0x01, 0x54,
            0xA7, 0xFA, 0x4D, 0xA0, 0xF3, 0x46, 0x99, 0xEC,
            0x3F, 0x92, 0xE5, 0x38, 0x8B, 0xDE, 0x31, 0x84,
            0xD7, 0x2A, 0x7D, 0xD0, 0x23, 0x76, 0xC9, 0x1C
        };

        private static readonly byte[] s_iv = new byte[8]
        {
            0x28, 0x8F, 0xF6, 0x5D, 0xC4, 0x2B, 0x92, 0xF9
        };

        [Fact]
        public void Salsa20_SetVector6_3_First16Bytes()
        {
            byte[] pbExpected = new byte[16]
            {
                0x5E, 0x5E, 0x71, 0xF9, 0x01, 0x99, 0x34, 0x03,
                0x04, 0xAB, 0xB2, 0x2A, 0x37, 0xB6, 0x62, 0x5B
            };

            byte[] pb = new byte[16];
            using (Salsa20Cipher c = new Salsa20Cipher(s_key, s_iv))
            {
                c.Encrypt(pb, 0, pb.Length);
            }

            Assert.Equal(pbExpected, pb);
        }

        [Fact]
        public void Salsa20_SetVector6_3_At64KBOffset()
        {
            // Seeks to position 65536 and reads 16 bytes
            byte[] pbExpected = new byte[16]
            {
                0xAB, 0xF3, 0x9A, 0x21, 0x0E, 0xEE, 0x89, 0x59,
                0x8B, 0x71, 0x33, 0x37, 0x70, 0x56, 0xC2, 0xFE
            };

            byte[] buf = new byte[16];
            using (Salsa20Cipher c = new Salsa20Cipher(s_key, s_iv))
            {
                // Advance past first 16 bytes
                c.Encrypt(buf, 0, buf.Length);
                // Advance to byte offset 65536
                byte[] skip = new byte[512];
                int pos = buf.Length;
                while (pos < 65536)
                {
                    int n = Math.Min(512, 65536 - pos);
                    c.Encrypt(skip, 0, n);
                    pos += n;
                }
                // Read expected block
                Array.Clear(buf, 0, buf.Length);
                c.Encrypt(buf, 0, buf.Length);
            }

            Assert.Equal(pbExpected, buf);
        }

        [Fact]
        public void Salsa20_SetVector6_3_At131008Offset()
        {
            // Seeks to position 131008 and reads 16 bytes
            byte[] pbExpected = new byte[16]
            {
                0x1B, 0xA8, 0x9D, 0xBD, 0x3F, 0x98, 0x83, 0x97,
                0x28, 0xF5, 0x67, 0x91, 0xD5, 0xB7, 0xCE, 0x23
            };

            byte[] buf = new byte[512];
            int pos = 0;
            using (Salsa20Cipher c = new Salsa20Cipher(s_key, s_iv))
            {
                while (pos < 131008)
                {
                    int n = Math.Min(512, 131008 - pos);
                    c.Encrypt(buf, 0, n);
                    pos += n;
                }
                byte[] result = new byte[16];
                c.Encrypt(result, 0, result.Length);
                Assert.Equal(pbExpected, result);
            }
        }

        [Fact]
        public void Salsa20_100DifferentKeys_ProduceDistinctOutputs()
        {
            // Sanity check: different IV values produce different keystreams
            HashSet<string> hs = new HashSet<string>();
            byte[] z = new byte[32];
            for (int i = 0; i < 100; ++i)
            {
                Array.Clear(z, 0, z.Length);
                using (Salsa20Cipher cI = new Salsa20Cipher(z, MemUtil.Int64ToBytes(i)))
                {
                    cI.Encrypt(z, 0, z.Length);
                }
                // Use base64 as a unique string key for the hash set
                Assert.True(hs.Add(Convert.ToBase64String(z)),
                    $"Salsa20 output collision at IV index {i}");
            }
        }
    }
}
