using System;
using System.Security.Cryptography;
using System.Text;

namespace KeePass.Platform.Unix.Shared
{
    /// <summary>
    /// Tracks ownership of the system clipboard by storing a SHA-256 hash of
    /// the last text placed on it by this process.
    ///
    /// Used by <see cref="Mac.MacClipboardService"/> and
    /// <see cref="Linux.LinuxClipboardService"/> to implement
    /// <c>ClearIfOwner</c> without native clipboard change-count APIs.
    ///
    /// Thread-safe via <c>lock</c>.
    /// </summary>
    internal sealed class ClipboardOwnerTracker
    {
        private byte[] _lastHash;
        private readonly object _lock = new object();

        /// <summary>Records the SHA-256 hash of <paramref name="text"/>.</summary>
        internal void Record(string text)
        {
            byte[] hash = ComputeHash(text);
            lock (_lock) { _lastHash = hash; }
        }

        /// <summary>Clears ownership tracking (call after <c>Clear()</c>).</summary>
        internal void Forget()
        {
            lock (_lock) { _lastHash = null; }
        }

        /// <summary>
        /// Returns <c>true</c> if the supplied <paramref name="currentText"/>
        /// matches the last text recorded via <see cref="Record"/>.
        /// </summary>
        internal bool IsOwner(string currentText)
        {
            byte[] recorded;
            lock (_lock) { recorded = _lastHash; }
            if (recorded == null) return false;

            byte[] current = ComputeHash(currentText);
            return current != null && ConstantTimeEquals(recorded, current);
        }

        private static byte[] ComputeHash(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            return SHA256.HashData(Encoding.UTF8.GetBytes(text));
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
