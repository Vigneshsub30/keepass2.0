using System;
using System.Linq;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Keys;
using KeePassLib.Security;
using Xunit;

namespace KeePass.Tests.Keys
{
    /// <summary>
    /// Tests for CompositeKey — the authentication pipeline that aggregates user
    /// key sources and derives the database encryption key.
    ///
    /// Tests cover: AddUserKey/RemoveUserKey, ContainsType, GenerateKey32 with
    /// AES-KDF and Argon2id, EqualsValue, cross-platform determinism, and Unicode
    /// passphrase handling.
    ///
    /// Cross-platform: all tests use pure-crypto KDFs with no Windows-specific APIs.
    /// </summary>
    public class CompositeKeyTests
    {
        // ── 1. AddUserKey / RemoveUserKey / ContainsType ──────────────────────

        [Fact]
        public void AddUserKey_KcpPassword_ContainsTypeReturnsTrue()
        {
            CompositeKey key = new CompositeKey();
            key.AddUserKey(new KcpPassword("testpass"));

            Assert.True(key.ContainsType(typeof(KcpPassword)));
        }

        [Fact]
        public void AddUserKey_IncrementsUserKeyCount()
        {
            CompositeKey key = new CompositeKey();
            Assert.Equal(0U, key.UserKeyCount);

            key.AddUserKey(new KcpPassword("p1"));
            Assert.Equal(1U, key.UserKeyCount);
        }

        [Fact]
        public void RemoveUserKey_DecrementsCount()
        {
            CompositeKey key = new CompositeKey();
            KcpPassword pw = new KcpPassword("p1");
            key.AddUserKey(pw);
            Assert.Equal(1U, key.UserKeyCount);

            key.RemoveUserKey(pw);
            Assert.Equal(0U, key.UserKeyCount);
            Assert.False(key.ContainsType(typeof(KcpPassword)));
        }

        // ── 2. GenerateKey32 — AES-KDF ────────────────────────────────────────

        [Fact]
        public void GenerateKey32_AesKdf_Returns32Bytes()
        {
            CompositeKey key = MakeKey("WO024-AesTest!");

            AesKdf aes = new AesKdf();
            KdfParameters p = aes.GetDefaultParameters();
            p.SetUInt64(AesKdf.ParamRounds, 10);
            p.SetByteArray(AesKdf.ParamSeed, new byte[32]);  // deterministic zero seed

            ProtectedBinary derived = key.GenerateKey32(p);
            Assert.NotNull(derived);
            Assert.Equal(32U, derived.Length);
        }

        [Fact]
        public void GenerateKey32_AesKdf_SameSeedAndPassword_Deterministic()
        {
            KdfParameters p = MakeAesKdfParams(10, new byte[32]);

            ProtectedBinary k1 = MakeKey("deterministic-pw").GenerateKey32(p);
            ProtectedBinary k2 = MakeKey("deterministic-pw").GenerateKey32(p);

            Assert.True(k1.Equals(k2, false),
                "AES-KDF should produce the same key bytes for the same password and seed");
        }

        [Fact]
        public void GenerateKey32_AesKdf_DifferentPasswords_ProduceDifferentKeys()
        {
            KdfParameters p = MakeAesKdfParams(10, new byte[32]);

            ProtectedBinary k1 = MakeKey("password-A").GenerateKey32(p);
            ProtectedBinary k2 = MakeKey("password-B").GenerateKey32(p);

            Assert.False(k1.Equals(k2, false),
                "Different passwords should produce different derived keys");
        }

        // ── 3. GenerateKey32 — Argon2id ──────────────────────────────────────

        [Fact]
        public void GenerateKey32_Argon2id_Returns32Bytes()
        {
            CompositeKey key = MakeKey("WO024-Argon2Test!");
            KdfParameters p = MakeArgon2idParams();

            ProtectedBinary derived = key.GenerateKey32(p);
            Assert.NotNull(derived);
            Assert.Equal(32U, derived.Length);
        }

        [Fact]
        public void GenerateKey32_Argon2id_SameSaltAndPassword_Deterministic()
        {
            // Use a fixed salt (Argon2 requires a non-null, non-random salt for determinism)
            byte[] salt = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            KdfParameters p = MakeArgon2idParams(salt);

            ProtectedBinary k1 = MakeKey("argon2-pw").GenerateKey32(p);
            ProtectedBinary k2 = MakeKey("argon2-pw").GenerateKey32(p);

            Assert.True(k1.Equals(k2, false),
                "Argon2id should produce identical keys for the same password and salt");
        }

        [Fact]
        public void GenerateKey32_Argon2id_DifferentPasswords_ProduceDifferentKeys()
        {
            byte[] salt = new byte[32];  // all zeros — deterministic
            KdfParameters p = MakeArgon2idParams(salt);

            ProtectedBinary k1 = MakeKey("pass-X").GenerateKey32(p);
            ProtectedBinary k2 = MakeKey("pass-Y").GenerateKey32(p);

            Assert.False(k1.Equals(k2, false),
                "Different passwords should produce different Argon2id keys");
        }

        // ── 4. EqualsValue ────────────────────────────────────────────────────

        [Fact]
        public void EqualsValue_SamePassword_ReturnsTrue()
        {
            CompositeKey a = MakeKey("shared-password");
            CompositeKey b = MakeKey("shared-password");
            Assert.True(a.EqualsValue(b));
        }

        [Fact]
        public void EqualsValue_DifferentPassword_ReturnsFalse()
        {
            CompositeKey a = MakeKey("password-A");
            CompositeKey b = MakeKey("password-B");
            Assert.False(a.EqualsValue(b));
        }

        // ── 5. Unicode passphrase cross-platform consistency ─────────────────

        [Fact]
        public void GenerateKey32_UnicodePassword_Deterministic()
        {
            const string unicodePass = "密码🔐сПасворд";
            byte[] salt = new byte[32];
            KdfParameters p = MakeArgon2idParams(salt);

            ProtectedBinary k1 = MakeKey(unicodePass).GenerateKey32(p);
            ProtectedBinary k2 = MakeKey(unicodePass).GenerateKey32(p);

            Assert.True(k1.Equals(k2, false),
                "Unicode passphrase should produce deterministic keys across calls");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static CompositeKey MakeKey(string password)
        {
            CompositeKey key = new CompositeKey();
            key.AddUserKey(new KcpPassword(password));
            return key;
        }

        private static KdfParameters MakeAesKdfParams(ulong rounds, byte[] seed)
        {
            AesKdf aes = new AesKdf();
            KdfParameters p = aes.GetDefaultParameters();
            p.SetUInt64(AesKdf.ParamRounds, rounds);
            p.SetByteArray(AesKdf.ParamSeed, seed);
            return p;
        }

        private static KdfParameters MakeArgon2idParams(byte[] salt = null)
        {
            Argon2Kdf kdf = new Argon2Kdf(Argon2Type.ID);
            KdfParameters p = kdf.GetDefaultParameters();
            p.SetUInt64(Argon2Kdf.ParamMemory,      4 * 1024);  // 4 MB minimum
            p.SetUInt64(Argon2Kdf.ParamIterations,  1);
            p.SetUInt32(Argon2Kdf.ParamParallelism, 1);
            if (salt != null)
                p.SetByteArray(Argon2Kdf.ParamSalt, salt);
            else
                kdf.Randomize(p);  // random salt when caller doesn't need determinism
            return p;
        }
    }
}
