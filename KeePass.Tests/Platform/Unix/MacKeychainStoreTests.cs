using System;
using KeePass.Core.Platform;
using KeePass.Platform.Unix.Mac;
using Xunit;

namespace KeePass.Tests.Platform.Unix
{
    /// <summary>
    /// Characterization tests for <see cref="MacKeychainStore"/>.
    /// Verifies contract surface without invoking the <c>security</c> CLI.
    /// </summary>
    public class MacKeychainStoreTests
    {
        [Fact]
        public void MacKeychainStore_IsSupported_ReturnsTrue()
        {
            var store = new MacKeychainStore();
            Assert.True(store.IsSupported);
        }

        // ── Store ─────────────────────────────────────────────────────────

        [Fact]
        public void MacKeychainStore_Store_NullKey_ThrowsArgumentNull()
        {
            var store = new MacKeychainStore();
            Assert.Throws<ArgumentNullException>(
                () => store.Store(null, new byte[] { 1 }));
        }

        [Fact]
        public void MacKeychainStore_Store_EmptyKey_ThrowsArgumentException()
        {
            var store = new MacKeychainStore();
            Assert.Throws<ArgumentException>(
                () => store.Store(string.Empty, new byte[] { 1 }));
        }

        [Fact]
        public void MacKeychainStore_Store_NullSecret_ThrowsArgumentNull()
        {
            var store = new MacKeychainStore();
            Assert.Throws<ArgumentNullException>(
                () => store.Store("mykey", null));
        }

        [Fact]
        public void MacKeychainStore_Store_EmptySecret_ThrowsArgumentException()
        {
            var store = new MacKeychainStore();
            Assert.Throws<ArgumentException>(
                () => store.Store("mykey", Array.Empty<byte>()));
        }

        // ── Retrieve ──────────────────────────────────────────────────────

        [Fact]
        public void MacKeychainStore_Retrieve_NullKey_ThrowsArgumentNull()
        {
            var store = new MacKeychainStore();
            Assert.Throws<ArgumentNullException>(() => store.Retrieve(null));
        }

        // ── Delete ────────────────────────────────────────────────────────

        [Fact]
        public void MacKeychainStore_Delete_NullKey_ThrowsArgumentNull()
        {
            var store = new MacKeychainStore();
            Assert.Throws<ArgumentNullException>(() => store.Delete(null));
        }

        [Fact]
        public void MacKeychainStore_ImplementsICredentialStore()
        {
            ICredentialStore store = new MacKeychainStore();
            Assert.NotNull(store);
        }
    }
}
