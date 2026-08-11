using System;
using KeePass.Core.Platform;
using KeePass.Platform.Unix.Linux;
using Xunit;

namespace KeePass.Tests.Platform.Unix
{
    /// <summary>
    /// Characterization tests for <see cref="LinuxSecretStore"/>.
    /// Verifies parameter-validation contracts without invoking <c>secret-tool</c>.
    /// Runs on all platforms.
    /// </summary>
    public class LinuxSecretStoreTests
    {
        [Fact]
        public void LinuxSecretStore_IsSupported_DoesNotThrow()
        {
            var store = new LinuxSecretStore();
            // Value depends on whether secret-tool is installed.
            bool _ = store.IsSupported;
        }

        // ── Store ─────────────────────────────────────────────────────────

        [Fact]
        public void LinuxSecretStore_Store_NullKey_ThrowsArgumentNull()
        {
            var store = new LinuxSecretStore();
            Assert.Throws<ArgumentNullException>(
                () => store.Store(null, new byte[] { 1 }));
        }

        [Fact]
        public void LinuxSecretStore_Store_EmptyKey_ThrowsArgumentException()
        {
            var store = new LinuxSecretStore();
            Assert.Throws<ArgumentException>(
                () => store.Store(string.Empty, new byte[] { 1 }));
        }

        [Fact]
        public void LinuxSecretStore_Store_NullSecret_ThrowsArgumentNull()
        {
            var store = new LinuxSecretStore();
            Assert.Throws<ArgumentNullException>(
                () => store.Store("mykey", null));
        }

        [Fact]
        public void LinuxSecretStore_Store_EmptySecret_ThrowsArgumentException()
        {
            var store = new LinuxSecretStore();
            Assert.Throws<ArgumentException>(
                () => store.Store("mykey", Array.Empty<byte>()));
        }

        // ── Retrieve ──────────────────────────────────────────────────────

        [Fact]
        public void LinuxSecretStore_Retrieve_NullKey_ThrowsArgumentNull()
        {
            var store = new LinuxSecretStore();
            Assert.Throws<ArgumentNullException>(() => store.Retrieve(null));
        }

        // ── Delete ────────────────────────────────────────────────────────

        [Fact]
        public void LinuxSecretStore_Delete_NullKey_ThrowsArgumentNull()
        {
            var store = new LinuxSecretStore();
            Assert.Throws<ArgumentNullException>(() => store.Delete(null));
        }

        [Fact]
        public void LinuxSecretStore_ImplementsICredentialStore()
        {
            ICredentialStore store = new LinuxSecretStore();
            Assert.NotNull(store);
        }
    }
}
