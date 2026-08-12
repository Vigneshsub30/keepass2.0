using KeePassLib.Cryptography.KeyDerivation;
using Xunit;

namespace KeePass.Tests.Cryptography
{
    /// <summary>
    /// xUnit promotion of the Argon2 self-test vectors from SelfTest.TestArgon2().
    /// Official Argon2 1.3 reference package vectors for Argon2d and Argon2id.
    /// </summary>
    public class Argon2Tests
    {
        // ── Common parameter builder ─────────────────────────────────────────
        private static KdfParameters BuildParams(Argon2Kdf kdf, uint uVersion,
            ulong uMemory, ulong uIterations, uint uParallelism,
            byte[] pbSalt, byte[] pbSecretKey, byte[] pbAssocData)
        {
            KdfParameters p = kdf.GetDefaultParameters();
            kdf.Randomize(p);
            p.SetUInt32(Argon2Kdf.ParamVersion, uVersion);
            p.SetUInt64(Argon2Kdf.ParamMemory, uMemory);
            p.SetUInt64(Argon2Kdf.ParamIterations, uIterations);
            p.SetUInt32(Argon2Kdf.ParamParallelism, uParallelism);
            p.SetByteArray(Argon2Kdf.ParamSalt, pbSalt);
            if (pbSecretKey != null) p.SetByteArray(Argon2Kdf.ParamSecretKey, pbSecretKey);
            if (pbAssocData != null) p.SetByteArray(Argon2Kdf.ParamAssocData, pbAssocData);
            return p;
        }

        // ── Argon2d vectors ──────────────────────────────────────────────────

        // Official Argon2 1.3 reference — Argon2d version 1.3
        [Fact]
        public void Argon2d_Rfc_V13_OfficialVector()
        {
            byte[] pbMsg = new byte[32];
            for (int i = 0; i < pbMsg.Length; ++i) pbMsg[i] = 1;

            byte[] pbSalt = new byte[16];
            for (int i = 0; i < pbSalt.Length; ++i) pbSalt[i] = 2;

            byte[] pbKey = new byte[8];
            for (int i = 0; i < pbKey.Length; ++i) pbKey[i] = 3;

            byte[] pbAssoc = new byte[12];
            for (int i = 0; i < pbAssoc.Length; ++i) pbAssoc[i] = 4;

            byte[] pbExpected = new byte[32]
            {
                0x51, 0x2B, 0x39, 0x1B, 0x6F, 0x11, 0x62, 0x97,
                0x53, 0x71, 0xD3, 0x09, 0x19, 0x73, 0x42, 0x94,
                0xF8, 0x68, 0xE3, 0xBE, 0x39, 0x84, 0xF3, 0xC1,
                0xA1, 0x3A, 0x4D, 0xB9, 0xFA, 0xBE, 0x4A, 0xCB
            };

            Argon2Kdf kdf = new Argon2Kdf(Argon2Type.D);
            KdfParameters p = BuildParams(kdf, 0x13, 32 * 1024, 3, 4, pbSalt, pbKey, pbAssoc);
            byte[] pb = kdf.Transform(pbMsg, p);

            Assert.Equal(pbExpected, pb);
        }

        // Official Argon2 1.3 reference — Argon2d version 1.0
        [Fact]
        public void Argon2d_Rfc_V10_OfficialVector()
        {
            byte[] pbMsg = new byte[32];
            for (int i = 0; i < pbMsg.Length; ++i) pbMsg[i] = 1;

            byte[] pbSalt = new byte[16];
            for (int i = 0; i < pbSalt.Length; ++i) pbSalt[i] = 2;

            byte[] pbKey = new byte[8];
            for (int i = 0; i < pbKey.Length; ++i) pbKey[i] = 3;

            byte[] pbAssoc = new byte[12];
            for (int i = 0; i < pbAssoc.Length; ++i) pbAssoc[i] = 4;

            byte[] pbExpected = new byte[32]
            {
                0x96, 0xA9, 0xD4, 0xE5, 0xA1, 0x73, 0x40, 0x92,
                0xC8, 0x5E, 0x29, 0xF4, 0x10, 0xA4, 0x59, 0x14,
                0xA5, 0xDD, 0x1F, 0x5C, 0xBF, 0x08, 0xB2, 0x67,
                0x0D, 0xA6, 0x8A, 0x02, 0x85, 0xAB, 0xF3, 0x2B
            };

            Argon2Kdf kdf = new Argon2Kdf(Argon2Type.D);
            KdfParameters p = BuildParams(kdf, 0x10, 32 * 1024, 3, 4, pbSalt, pbKey, pbAssoc);
            byte[] pb = kdf.Transform(pbMsg, p);

            Assert.Equal(pbExpected, pb);
        }

        // phc-winner-argon2-20151206 reference — Argon2d version 1.0, 16 MB
        [Fact]
        public void Argon2d_PhcWinner_V10_16MBMemory()
        {
            byte[] pbMsg = new byte[32];
            for (int i = 0; i < pbMsg.Length; ++i) pbMsg[i] = 1;

            byte[] pbSalt = new byte[16];
            for (int i = 0; i < pbSalt.Length; ++i) pbSalt[i] = 2;

            byte[] pbKey = new byte[8];
            for (int i = 0; i < pbKey.Length; ++i) pbKey[i] = 3;

            byte[] pbAssoc = new byte[12];
            for (int i = 0; i < pbAssoc.Length; ++i) pbAssoc[i] = 4;

            byte[] pbExpected = new byte[32]
            {
                0x57, 0xB0, 0x61, 0x3B, 0xFD, 0xD4, 0x13, 0x1A,
                0x0C, 0x34, 0x88, 0x34, 0xC6, 0x72, 0x9C, 0x2C,
                0x72, 0x29, 0x92, 0x1E, 0x6B, 0xBA, 0x37, 0x66,
                0x5D, 0x97, 0x8C, 0x4F, 0xE7, 0x17, 0x5E, 0xD2
            };

            Argon2Kdf kdf = new Argon2Kdf(Argon2Type.D);
            KdfParameters p = BuildParams(kdf, 0x10, 16 * 1024, 3, 4, pbSalt, pbKey, pbAssoc);
            byte[] pb = kdf.Transform(pbMsg, p);

            Assert.Equal(pbExpected, pb);
        }

        // ── Argon2id vectors ─────────────────────────────────────────────────

        // Official Argon2 1.3 reference — Argon2id version 1.3
        [Fact]
        public void Argon2id_Rfc_V13_OfficialVector()
        {
            byte[] pbMsg = new byte[32];
            for (int i = 0; i < pbMsg.Length; ++i) pbMsg[i] = 1;

            byte[] pbSalt = new byte[16];
            for (int i = 0; i < pbSalt.Length; ++i) pbSalt[i] = 2;

            byte[] pbKey = new byte[8];
            for (int i = 0; i < pbKey.Length; ++i) pbKey[i] = 3;

            byte[] pbAssoc = new byte[12];
            for (int i = 0; i < pbAssoc.Length; ++i) pbAssoc[i] = 4;

            byte[] pbExpected = new byte[32]
            {
                0x0D, 0x64, 0x0D, 0xF5, 0x8D, 0x78, 0x76, 0x6C,
                0x08, 0xC0, 0x37, 0xA3, 0x4A, 0x8B, 0x53, 0xC9,
                0xD0, 0x1E, 0xF0, 0x45, 0x2D, 0x75, 0xB6, 0x5E,
                0xB5, 0x25, 0x20, 0xE9, 0x6B, 0x01, 0xE6, 0x59
            };

            Argon2Kdf kdf = new Argon2Kdf(Argon2Type.ID);
            KdfParameters p = BuildParams(kdf, 0x13, 32 * 1024, 3, 4, pbSalt, pbKey, pbAssoc);
            byte[] pb = kdf.Transform(pbMsg, p);

            Assert.Equal(pbExpected, pb);
        }

        // Official argon2 application — Argon2id version 1.3, real passphrase
        [Fact]
        public void Argon2id_AppTool_V13_1MBMemory()
        {
            byte[] pbMsg = KeePassLib.Utility.StrUtil.Utf8.GetBytes("ABC1234");
            byte[] pbSalt = KeePassLib.Utility.StrUtil.Utf8.GetBytes("somesalt");

            byte[] pbExpected = new byte[32]
            {
                0x32, 0x5E, 0x67, 0x27, 0x0D, 0xB7, 0xAD, 0x0A,
                0x7D, 0xD9, 0x0E, 0xEC, 0x46, 0x5C, 0x80, 0x61,
                0x0F, 0x04, 0xE2, 0x67, 0x8E, 0xED, 0xF7, 0xE0,
                0xEF, 0x29, 0x5B, 0x3B, 0x42, 0x5A, 0xCF, 0x7A
            };

            Argon2Kdf kdf = new Argon2Kdf(Argon2Type.ID);
            KdfParameters p = kdf.GetDefaultParameters();
            p.SetUInt64(Argon2Kdf.ParamMemory, (1 << 10) * 1024); // 1 MB
            p.SetUInt64(Argon2Kdf.ParamIterations, 4);
            p.SetUInt32(Argon2Kdf.ParamParallelism, 4);
            p.SetByteArray(Argon2Kdf.ParamSalt, pbSalt);
            byte[] pb = kdf.Transform(pbMsg, p);

            Assert.Equal(pbExpected, pb);
        }
    }
}
