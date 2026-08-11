using KeePassLib.Cryptography;
using Xunit;

namespace KeePass.Tests
{
    /// <summary>
    /// Characterization test that verifies all KeePassLib cryptographic self-test
    /// vectors continue to pass after API replacements (WO-006).
    /// </summary>
    public class SelfTestCharacterization
    {
        [Fact]
        public void SelfTestPerform_AllVectorsPass()
        {
            // SelfTest.Perform() throws KeePassLib.Utility.UnexpectedTypeException
            // (a subclass of Exception) if any crypto vector fails.
            SelfTest.Perform();
        }
    }
}
