using System.Security.Cryptography;
using Xunit;

namespace KeePass.Tests.Cryptography
{
    /// <summary>
    /// xUnit promotion of the AES self-test vector from SelfTest.TestAes()
    /// (NIST ECB test vector #356 from official set 6, vector 3).
    /// </summary>
    public class AesTests
    {
        [Fact]
        public void Aes256_EcbVector356_ProducesExpectedCiphertext()
        {
            byte[] pbKey = new byte[32];    // all zeros
            byte[] pbIV  = new byte[16];    // all zeros (unused in ECB)
            byte[] pbData = new byte[16];
            pbData[0] = 0x04;

            byte[] pbExpected = new byte[16]
            {
                0x75, 0xD1, 0x1B, 0x0E, 0x3A, 0x68, 0xC4, 0x22,
                0x3D, 0x88, 0xDB, 0xF0, 0x17, 0x97, 0x7D, 0xD7
            };

            using (Aes a = Aes.Create())
            {
                a.KeySize = 256;
                a.Mode = CipherMode.ECB;
                a.Padding = PaddingMode.None;
                using (ICryptoTransform t = a.CreateEncryptor(pbKey, pbIV))
                {
                    t.TransformBlock(pbData, 0, 16, pbData, 0);
                }
            }

            Assert.Equal(pbExpected, pbData);
        }
    }
}
