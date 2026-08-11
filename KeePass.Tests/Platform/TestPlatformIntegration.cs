using System;
using System.Collections.Generic;
using KeePass.Core.Platform;

namespace KeePass.Tests.Platform
{
    /// <summary>
    /// Configurable stub implementation of <see cref="IPlatformIntegration"/>
    /// for use in unit tests.  All capability flags default to false; tests may
    /// set them to true to simulate specific platform tiers.
    ///
    /// Shared across the test suite so downstream WO test classes can inject a
    /// known platform configuration without depending on the real host OS.
    /// </summary>
    internal sealed class TestPlatformIntegration : IPlatformIntegration
    {
        public PlatformId PlatformId { get; set; } = PlatformId.Windows;

        /// <summary>Defaults to <c>true</c> (always-on-top works on Windows).</summary>
        public bool SupportsAlwaysOnTop { get; set; } = true;

        /// <summary>Defaults to <c>false</c> (Windows enforces min sizes natively).</summary>
        public bool RequiresWindowMinSizeEnforcement { get; set; } = false;

        /// <summary>
        /// Configurable capability tiers.  Defaults to
        /// <see cref="PlatformCapabilityTier.Unsupported"/> for all capabilities
        /// so tests that do not care about a capability get a safe default.
        /// </summary>
        public Dictionary<PlatformCapability, PlatformCapabilityTier> CapabilityTiers { get; } =
            new Dictionary<PlatformCapability, PlatformCapabilityTier>();

        public IClipboardService     Clipboard        { get; set; }
        public ICredentialStore      CredentialStore  { get; set; }
        public IAutoTypeService      AutoType         { get; set; }
        public IScreenProtectionService ScreenProtection { get; set; }

        public TestPlatformIntegration()
        {
            Clipboard        = new TestClipboardService();
            CredentialStore  = new TestCredentialStore();
            AutoType         = new TestAutoTypeService();
            ScreenProtection = new TestScreenProtectionService();
        }

        /// <inheritdoc/>
        public PlatformCapabilityTier GetCapabilityTier(PlatformCapability capability)
        {
            PlatformCapabilityTier tier;
            if(CapabilityTiers.TryGetValue(capability, out tier))
                return tier;
            return PlatformCapabilityTier.Unsupported;
        }

        // ── Nested stub implementations ────────────────────────────────────────

        internal sealed class TestClipboardService : IClipboardService
        {
            public bool IsSupported { get; set; } = false;

            private string _text;
            private string _ownerText;

            public void SetText(string text)
            {
                ThrowIfUnsupported();
                _text = text;
                _ownerText = text;
            }

            public string GetText()
            {
                ThrowIfUnsupported();
                return _text;
            }

            public void Clear()
            {
                ThrowIfUnsupported();
                _text = null;
                _ownerText = null;
            }

            public void ClearIfOwner()
            {
                if (_text != null && _text == _ownerText)
                {
                    _text = null;
                    _ownerText = null;
                }
            }

            public void SetWithAutoClear(string text, TimeSpan timeout)
            {
                ThrowIfUnsupported();
                _text = text;
                _ownerText = text;
                // In the test stub, no actual timer fires.
            }

            private void ThrowIfUnsupported()
            {
                if (!IsSupported)
                    throw new PlatformNotSupportedException("Clipboard is not supported in this test configuration.");
            }
        }

        internal sealed class TestCredentialStore : ICredentialStore
        {
            public bool IsSupported { get; set; } = false;

            private readonly Dictionary<string, byte[]> _store =
                new Dictionary<string, byte[]>();

            public void Store(string key, byte[] secret)
            {
                ThrowIfUnsupported();
                if (key == null) throw new ArgumentNullException("key");
                if (secret == null || secret.Length == 0)
                    throw new ArgumentNullException("secret");
                _store[key] = secret;
            }

            public byte[] Retrieve(string key)
            {
                ThrowIfUnsupported();
                if (key == null) throw new ArgumentNullException("key");
                _store.TryGetValue(key, out byte[] value);
                return value;
            }

            public void Delete(string key)
            {
                ThrowIfUnsupported();
                if (key == null) throw new ArgumentNullException("key");
                _store.Remove(key);
            }

            private void ThrowIfUnsupported()
            {
                if (!IsSupported)
                    throw new PlatformNotSupportedException("CredentialStore is not supported in this test configuration.");
            }
        }

        internal sealed class TestAutoTypeService : IAutoTypeService
        {
            public bool IsSupported { get; set; } = false;

            /// <summary>Records the last <see cref="AutoTypeContext"/> passed to <see cref="PerformAutoType"/>.</summary>
            public AutoTypeContext LastContext { get; private set; }

            public void PerformAutoType(AutoTypeContext ctx)
            {
                if (ctx == null) throw new ArgumentNullException("ctx");
                if (!IsSupported)
                    throw new PlatformNotSupportedException("AutoType is not supported in this test configuration.");
                LastContext = ctx;
            }
        }

        internal sealed class TestScreenProtectionService : IScreenProtectionService
        {
            public bool IsSupported { get; set; } = false;

            public bool IsEnabled { get; private set; }

            public void Enable()  { if (IsSupported) IsEnabled = true; }
            public void Disable() { if (IsSupported) IsEnabled = false; }
        }
    }
}
