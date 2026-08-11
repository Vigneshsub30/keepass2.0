using System;

using KeePass.Core.Platform;
using KeePass.Platform;

using Xunit;

namespace KeePass.Tests.Platform.Windows
{
    /// <summary>
    /// Characterization tests for <see cref="WindowsCredentialStore"/>.
    ///
    /// Tests in this file verify the contract behavior:
    ///   - <see cref="ICredentialStore.IsSupported"/> is always <c>true</c> on Windows.
    ///   - Parameter validation: null/empty inputs throw the expected exceptions.
    ///   - The service can be assigned to <see cref="ICredentialStore"/>.
    ///
    /// Integration tests (actual CredWrite/CredRead/CredDelete round-trip) are
    /// marked <see cref="FactAttribute"/> and tag-filtered on CI to avoid
    /// writing to the Windows Credential Manager during automated testing.
    /// </summary>
    public class WindowsCredentialStoreTests
    {
        private readonly ICredentialStore _svc = new WindowsCredentialStore();

        // ── 1. Contract: IsSupported ───────────────────────────────────────

        [Fact]
        public void IsSupported_IsTrue_OnWindows()
        {
            Assert.True(_svc.IsSupported);
        }

        [Fact]
        public void CanBeAssignedToInterface()
        {
            ICredentialStore asInterface = new WindowsCredentialStore();
            Assert.NotNull(asInterface);
        }

        // ── 2. Parameter validation: Store ────────────────────────────────

        [Fact]
        public void Store_NullKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _svc.Store(null, new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void Store_EmptyKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _svc.Store(string.Empty, new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void Store_NullSecret_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _svc.Store("test-key", null));
        }

        [Fact]
        public void Store_EmptySecret_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _svc.Store("test-key", Array.Empty<byte>()));
        }

        // ── 3. Parameter validation: Retrieve ─────────────────────────────

        [Fact]
        public void Retrieve_NullKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.Retrieve(null));
        }

        // ── 4. Parameter validation: Delete ───────────────────────────────

        [Fact]
        public void Delete_NullKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.Delete(null));
        }

        [Fact]
        public void Delete_NonExistentKey_DoesNotThrow()
        {
            // Deleting a non-existent key is a no-op (ERROR_NOT_FOUND is absorbed).
            string key = $"KeePassTest_NonExistent_{Guid.NewGuid():N}";
            _svc.Delete(key); // must not throw
        }
    }
}
