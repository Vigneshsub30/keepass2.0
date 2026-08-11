using Microsoft.Extensions.Logging;

namespace KeePass.Core.Infrastructure
{
    /// <summary>
    /// Structured-logging event-ID constants for KeePass.
    ///
    /// Event ID ranges:
    /// <list type="table">
    ///   <listheader><term>Range</term><description>Category</description></listheader>
    ///   <item><term>1000–1099</term><description>IO / File operations</description></item>
    ///   <item><term>1100–1199</term><description>Cryptographic operations</description></item>
    ///   <item><term>1200–1299</term><description>Serialization (KDBX read/write)</description></item>
    ///   <item><term>1300–1399</term><description>Plugin system</description></item>
    ///   <item><term>1400–1499</term><description>Import / Export</description></item>
    ///   <item><term>1500–1599</term><description>Platform integration</description></item>
    /// </list>
    /// </summary>
    public static class LoggingConstants
    {
        // ── IO / File ──────────────────────────────────────────────────────

        /// <summary>A file-system IO operation failed.</summary>
        public static readonly EventId IoError = new EventId(1000, "IoError");

        /// <summary>An atomic file transaction failed or had to roll back.</summary>
        public static readonly EventId FileTransactionError = new EventId(1001, "FileTransactionError");

        /// <summary>An outbound network / remote connection failed.</summary>
        public static readonly EventId NetworkError = new EventId(1002, "NetworkError");

        /// <summary>An HTTP request to a remote KeePass database failed.</summary>
        public static readonly EventId HttpRequestError = new EventId(1003, "HttpRequestError");

        // ── Cryptography ──────────────────────────────────────────────────

        /// <summary>A cryptographic operation (hash, cipher, KDF) failed.</summary>
        public static readonly EventId CryptoError = new EventId(1100, "CryptoError");

        /// <summary>Master-key derivation encountered an unexpected error.</summary>
        public static readonly EventId KeyDerivationError = new EventId(1101, "KeyDerivationError");

        // ── Serialization ─────────────────────────────────────────────────

        /// <summary>KDBX database read encountered a non-fatal format warning.</summary>
        public static readonly EventId KdbxReadWarning = new EventId(1200, "KdbxReadWarning");

        /// <summary>KDBX database read failed with an unrecoverable error.</summary>
        public static readonly EventId KdbxReadError = new EventId(1201, "KdbxReadError");

        /// <summary>KDBX database write encountered a non-fatal error.</summary>
        public static readonly EventId KdbxWriteError = new EventId(1202, "KdbxWriteError");

        // ── Plugin ────────────────────────────────────────────────────────

        /// <summary>A plugin threw an unhandled exception during an operation.</summary>
        public static readonly EventId PluginError = new EventId(1300, "PluginError");

        // ── Import / Export ───────────────────────────────────────────────

        /// <summary>A format import/export encountered an unexpected error.</summary>
        public static readonly EventId ImportExportError = new EventId(1400, "ImportExportError");

        // ── Platform ──────────────────────────────────────────────────────

        /// <summary>A platform service (clipboard, credential store, etc.) failed.</summary>
        public static readonly EventId PlatformError = new EventId(1500, "PlatformError");
    }
}
