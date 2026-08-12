namespace KeePass.Core.Platform
{
    /// <summary>
    /// Provides access to the OS-native credential store for caching sensitive
    /// key material across sessions.
    ///
    /// Platform implementations:
    /// - Windows: Windows Credential Manager (CredRead/CredWrite)
    /// - macOS:   Keychain Services
    /// - Linux:   libsecret / Secret Service D-Bus API
    ///
    /// All byte[] values stored or retrieved are treated as opaque blobs by
    /// this interface; callers are responsible for encryption before storage.
    /// </summary>
    public interface ICredentialStore
    {
        /// <summary>
        /// Gets a value indicating whether the OS credential store is available
        /// on the current platform.  Callers must check this before calling any
        /// other member; calling an unsupported member throws
        /// <see cref="System.PlatformNotSupportedException"/>.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Stores <paramref name="secret"/> under <paramref name="key"/> in
        /// the OS credential store.  Replaces any existing value for the key.
        /// </summary>
        /// <param name="key">Application-defined identifier for the secret.</param>
        /// <param name="secret">Secret bytes to store. Must not be null or empty.</param>
        /// <exception cref="System.PlatformNotSupportedException">
        /// Thrown if <see cref="IsSupported"/> is false.
        /// </exception>
        void Store(string key, byte[] secret);

        /// <summary>
        /// Retrieves the secret stored under <paramref name="key"/>, or
        /// <c>null</c> if no entry exists.
        /// </summary>
        /// <param name="key">Application-defined identifier.</param>
        /// <exception cref="System.PlatformNotSupportedException">
        /// Thrown if <see cref="IsSupported"/> is false.
        /// </exception>
        byte[] Retrieve(string key);

        /// <summary>
        /// Deletes the entry for <paramref name="key"/>.  No-op if the key
        /// does not exist.
        /// </summary>
        /// <param name="key">Application-defined identifier.</param>
        /// <exception cref="System.PlatformNotSupportedException">
        /// Thrown if <see cref="IsSupported"/> is false.
        /// </exception>
        void Delete(string key);
    }
}
