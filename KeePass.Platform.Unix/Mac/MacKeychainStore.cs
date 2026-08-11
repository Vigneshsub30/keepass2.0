using System;
using System.Globalization;
using System.Text;

using KeePass.Core.Platform;
using KeePass.Platform.Unix.Shared;

namespace KeePass.Platform.Unix.Mac
{
    /// <summary>
    /// macOS implementation of <see cref="ICredentialStore"/> backed by
    /// macOS Keychain Services via the <c>security</c> CLI tool.
    ///
    /// Commands used:
    /// <list type="bullet">
    ///   <item>
    ///     <c>security add-generic-password -a KeePass -s &lt;key&gt; -w &lt;hex&gt; -U</c>
    ///     (the <c>-U</c> flag upserts — updates the item if it already exists)
    ///   </item>
    ///   <item><c>security find-generic-password -a KeePass -s &lt;key&gt; -w</c></item>
    ///   <item><c>security delete-generic-password -a KeePass -s &lt;key&gt;</c></item>
    /// </list>
    ///
    /// Secrets are stored as lowercase hex strings to avoid encoding ambiguities
    /// with the Keychain password field.
    /// </summary>
    public sealed class MacKeychainStore : ICredentialStore
    {
        private const string AccountName = "KeePass";

        /// <inheritdoc/>
        public bool IsSupported => true;

        /// <inheritdoc/>
        public void Store(string key, byte[] secret)
        {
            if (key    == null) throw new ArgumentNullException("key");
            if (key.Length == 0) throw new ArgumentException("Key must not be empty.", "key");
            if (secret == null) throw new ArgumentNullException("secret");
            if (secret.Length == 0) throw new ArgumentException("Secret must not be empty.", "secret");

            string hex  = BytesToHex(secret);
            string args = string.Format(
                "add-generic-password -a {0} -s {1} -w {2} -U",
                Quote(AccountName), Quote(key), Quote(hex));

            bool ok = ProcessRunner.RunSilent("security", args);
            if (!ok)
                throw new InvalidOperationException(
                    string.Format("security add-generic-password failed for key '{0}'.", key));
        }

        /// <inheritdoc/>
        public byte[] Retrieve(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            string args = string.Format(
                "find-generic-password -a {0} -s {1} -w",
                Quote(AccountName), Quote(key));

            string hex = ProcessRunner.Run("security", args);
            if (string.IsNullOrWhiteSpace(hex)) return null;
            return HexToBytes(hex.Trim());
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            string args = string.Format(
                "delete-generic-password -a {0} -s {1}",
                Quote(AccountName), Quote(key));

            // Exit code 44 means "item not found" — treat as no-op.
            ProcessRunner.RunSilent("security", args);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static string Quote(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string BytesToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0) return null;
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out result[i]))
                    return null;
            }
            return result;
        }
    }
}
