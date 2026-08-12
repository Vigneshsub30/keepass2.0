using System;
using System.Globalization;
using System.Text;

using KeePass.Core.Platform;
using KeePass.Platform.Unix.Shared;

namespace KeePass.Platform.Unix.Linux
{
    /// <summary>
    /// Linux implementation of <see cref="ICredentialStore"/> backed by the
    /// libsecret D-Bus Secret Service API via the <c>secret-tool</c> CLI.
    ///
    /// Commands used:
    /// <list type="bullet">
    ///   <item>
    ///     <c>secret-tool store --label='KeePass' service KeePass account &lt;key&gt;</c>
    ///     (reads the secret from stdin)
    ///   </item>
    ///   <item>
    ///     <c>secret-tool lookup service KeePass account &lt;key&gt;</c>
    ///   </item>
    ///   <item>
    ///     <c>secret-tool clear service KeePass account &lt;key&gt;</c>
    ///   </item>
    /// </list>
    ///
    /// Secrets are stored as lowercase hex strings (same convention as
    /// <see cref="Mac.MacKeychainStore"/>) to avoid encoding issues with the
    /// Secret Service text payload.
    ///
    /// <see cref="IsSupported"/> returns <c>true</c> only when
    /// <c>secret-tool</c> is found on PATH.
    /// </summary>
    public sealed class LinuxSecretStore : ICredentialStore
    {
        private bool? _supported;
        private readonly object _supportLock = new object();

        private const string ServiceAttr = "service";
        private const string ServiceVal  = "KeePass";
        private const string AccountAttr = "account";

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <c>true</c> only when <c>secret-tool</c> is on PATH *and* a
        /// D-Bus session bus is running (indicated by the
        /// <c>DBUS_SESSION_BUS_ADDRESS</c> environment variable).  Without an
        /// active session bus secret-tool will fail at runtime even when the
        /// binary is installed — as is the case on headless CI runners.
        /// </remarks>
        public bool IsSupported
        {
            get
            {
                lock (_supportLock)
                {
                    if (_supported == null)
                    {
                        bool hasTool = ProcessRunner.Run("which", "secret-tool") != null;
                        bool hasSession = !string.IsNullOrEmpty(
                            Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));
                        _supported = hasTool && hasSession;
                    }
                    return _supported.Value;
                }
            }
        }

        /// <inheritdoc/>
        public void Store(string key, byte[] secret)
        {
            if (key    == null) throw new ArgumentNullException("key");
            if (key.Length == 0) throw new ArgumentException("Key must not be empty.", "key");
            if (secret == null) throw new ArgumentNullException("secret");
            if (secret.Length == 0) throw new ArgumentException("Secret must not be empty.", "secret");
            RequireSupport();

            string hex  = BytesToHex(secret);
            // secret-tool reads the secret value from stdin.
            string args = string.Format(
                "store --label=KeePass {0} {1} {2} {3}",
                ServiceAttr, Quote(ServiceVal), AccountAttr, Quote(key));

            bool ok = ProcessRunner.RunSilent("secret-tool", args, stdinData: hex);
            if (!ok)
                throw new InvalidOperationException(
                    string.Format("secret-tool store failed for key '{0}'.", key));
        }

        /// <inheritdoc/>
        public byte[] Retrieve(string key)
        {
            if (key == null) throw new ArgumentNullException("key");
            RequireSupport();

            string args = string.Format(
                "lookup {0} {1} {2} {3}",
                ServiceAttr, Quote(ServiceVal), AccountAttr, Quote(key));

            string hex = ProcessRunner.Run("secret-tool", args);
            if (string.IsNullOrWhiteSpace(hex)) return null;
            return HexToBytes(hex.Trim());
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            if (key == null) throw new ArgumentNullException("key");
            RequireSupport();

            string args = string.Format(
                "clear {0} {1} {2} {3}",
                ServiceAttr, Quote(ServiceVal), AccountAttr, Quote(key));

            // "clear" exits 0 even if nothing matched.
            ProcessRunner.RunSilent("secret-tool", args);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private void RequireSupport()
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException(
                    "secret-tool is not installed. " +
                    "Install the libsecret-tools package.");
        }

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
