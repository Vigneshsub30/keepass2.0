using System;

namespace KeePass.Core.Platform
{
    /// <summary>
    /// Safe-defaults implementation of <see cref="IPlatformIntegration"/> used
    /// when the DI container has not yet been built (e.g. during early startup)
    /// or in unit tests that do not exercise platform-specific code.
    ///
    /// <para>All capability flags return values appropriate for the current OS
    /// at the time of construction; no platform APIs are invoked.  Sub-services
    /// (<see cref="Clipboard"/>, <see cref="CredentialStore"/>, etc.) are
    /// unsupported stubs that throw <see cref="PlatformNotSupportedException"/>.</para>
    ///
    /// <para>Prefer injecting the real <see cref="IPlatformIntegration"/> from
    /// the DI container.  This class exists solely as a guard against
    /// null-reference exceptions when the container is unavailable.</para>
    /// </summary>
    public sealed class FallbackPlatformIntegration : IPlatformIntegration
    {
        /// <summary>The single reusable instance.</summary>
        public static readonly FallbackPlatformIntegration Instance =
            new FallbackPlatformIntegration();

        private FallbackPlatformIntegration() { }

        /// <inheritdoc/>
        public PlatformId PlatformId
        {
            get
            {
                if(System.Runtime.InteropServices.RuntimeInformation
                    .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return PlatformId.Windows;
                if(System.Runtime.InteropServices.RuntimeInformation
                    .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                    return PlatformId.MacOS;
                return PlatformId.Linux;
            }
        }

        /// <inheritdoc/>
        /// <remarks>Conservative default: <c>true</c> everywhere except Linux.</remarks>
        public bool SupportsAlwaysOnTop =>
            PlatformId != PlatformId.Linux;

        /// <inheritdoc/>
        /// <remarks>Conservative default: <c>true</c> on Linux, <c>false</c> elsewhere.</remarks>
        public bool RequiresWindowMinSizeEnforcement =>
            PlatformId == PlatformId.Linux;

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <see cref="PlatformCapabilityTier.Unsupported"/> for all
        /// capabilities because this implementation is a safe guard — no platform
        /// APIs are available at the time it is used.
        /// </remarks>
        public PlatformCapabilityTier GetCapabilityTier(PlatformCapability capability)
        {
            return PlatformCapabilityTier.Unsupported;
        }

        /// <inheritdoc/>
        public IClipboardService Clipboard => UnsupportedClipboard.Instance;

        /// <inheritdoc/>
        public ICredentialStore CredentialStore => UnsupportedCredentialStore.Instance;

        /// <inheritdoc/>
        public IAutoTypeService AutoType => new NullAutoTypeService();

        /// <inheritdoc/>
        public IScreenProtectionService ScreenProtection => new NullScreenProtectionService();

        // ── Unsupported stubs ──────────────────────────────────────────────

        private sealed class UnsupportedClipboard : IClipboardService
        {
            internal static readonly UnsupportedClipboard Instance = new UnsupportedClipboard();

            public bool IsSupported => false;

            public void SetText(string text)
            {
                throw new PlatformNotSupportedException(
                    "Clipboard is not available before DI container is built.");
            }

            public string GetText()
            {
                throw new PlatformNotSupportedException(
                    "Clipboard is not available before DI container is built.");
            }

            public void Clear()
            {
                throw new PlatformNotSupportedException(
                    "Clipboard is not available before DI container is built.");
            }

            public void ClearIfOwner() { /* safe no-op */ }

            public void SetWithAutoClear(string text, TimeSpan timeout)
            {
                throw new PlatformNotSupportedException(
                    "Clipboard is not available before DI container is built.");
            }
        }

        private sealed class UnsupportedCredentialStore : ICredentialStore
        {
            internal static readonly UnsupportedCredentialStore Instance =
                new UnsupportedCredentialStore();

            public bool IsSupported => false;

            public void Store(string key, byte[] secret)
            {
                throw new PlatformNotSupportedException(
                    "Credential store is not available before DI container is built.");
            }

            public byte[] Retrieve(string key)
            {
                throw new PlatformNotSupportedException(
                    "Credential store is not available before DI container is built.");
            }

            public void Delete(string key)
            {
                throw new PlatformNotSupportedException(
                    "Credential store is not available before DI container is built.");
            }
        }
    }
}
