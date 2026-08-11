using KeePassLib.Cryptography;
using KeePassLib.Utility;
using Xunit;

namespace KeePass.Tests.Cryptography
{
    /// <summary>
    /// xUnit promotion of the HOTP self-test vectors from SelfTest.TestHmacOtp().
    /// HOTP vectors from RFC 4226 Appendix D.
    ///
    /// Note: TOTP (SHA-256/SHA-512) vectors from RFC 6238 are exercised
    /// transitively by SelfTestCharacterization.SelfTestPerform_AllVectorsPass,
    /// which calls SelfTest.Perform() → TestHmacOtp() in Debug builds.
    /// </summary>
    public class HmacOtpTests
    {
        // RFC 4226 Appendix D — HOTP with SHA-1
        // Secret = ASCII "12345678901234567890"
        private static readonly byte[] s_hotpSecret =
            StrUtil.Utf8.GetBytes("12345678901234567890");

        [Theory]
        [InlineData(0UL, "755224")]
        [InlineData(1UL, "287082")]
        [InlineData(2UL, "359152")]
        [InlineData(3UL, "969429")]
        [InlineData(4UL, "338314")]
        [InlineData(5UL, "254676")]
        [InlineData(6UL, "287922")]
        [InlineData(7UL, "162583")]
        [InlineData(8UL, "399871")]
        [InlineData(9UL, "520489")]
        public void HmacOtp_Rfc4226_CounterVectors(ulong counter, string expected)
        {
            string result = HmacOtp.Generate(s_hotpSecret, counter, 6, false, -1);
            Assert.Equal(expected, result);
        }
    }
}
