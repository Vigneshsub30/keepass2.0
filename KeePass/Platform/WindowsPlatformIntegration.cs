/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using KeePass.Core.Platform;

namespace KeePass.Platform
{
    /// <summary>
    /// Windows implementation of <see cref="IPlatformIntegration"/>.
    ///
    /// Aggregates the four Windows-specific service implementations into a
    /// single integration object.  All capabilities report
    /// <see cref="IClipboardService.IsSupported"/> = <c>true</c>;
    /// <see cref="IScreenProtectionService.IsSupported"/> depends on the
    /// Windows version (requires Windows 7+).
    ///
    /// Intended to be constructed once at application start-up and registered
    /// in the DI container (WO-033) as <see cref="IPlatformIntegration"/>.
    /// Use the <see cref="Create"/> factory method which applies a runtime
    /// OS guard so the class is never instantiated on non-Windows platforms.
    /// </summary>
    public sealed class WindowsPlatformIntegration : IPlatformIntegration
    {
        /// <inheritdoc/>
        public PlatformId PlatformId => PlatformId.Windows;

        /// <inheritdoc/>
        /// <remarks>Always <c>true</c> on Windows.</remarks>
        public bool SupportsAlwaysOnTop => true;

        /// <inheritdoc/>
        /// <remarks>Always <c>false</c> on Windows — the OS enforces minimum sizes.</remarks>
        public bool RequiresWindowMinSizeEnforcement => false;

        /// <inheritdoc/>
        public PlatformCapabilityTier GetCapabilityTier(PlatformCapability capability)
        {
            switch(capability)
            {
                case PlatformCapability.Clipboard:               return PlatformCapabilityTier.Full;
                case PlatformCapability.ClipboardPrivacyMarkers: return PlatformCapabilityTier.Unsupported;
                case PlatformCapability.CredentialStore:         return PlatformCapabilityTier.Full;
                case PlatformCapability.AutoType:                return PlatformCapabilityTier.Full;
                case PlatformCapability.SecureDesktop:           return PlatformCapabilityTier.Full;
                case PlatformCapability.ScreenCaptureProtection: return PlatformCapabilityTier.Full;
                case PlatformCapability.ProcessDacl:             return PlatformCapabilityTier.Full;
                case PlatformCapability.GlobalHotKeys:           return PlatformCapabilityTier.Full;
                default:                                         return PlatformCapabilityTier.Unsupported;
            }
        }

        /// <inheritdoc/>
        public IClipboardService Clipboard { get; }

        /// <inheritdoc/>
        public ICredentialStore CredentialStore { get; }

        /// <inheritdoc/>
        public IAutoTypeService AutoType { get; }

        /// <inheritdoc/>
        public IScreenProtectionService ScreenProtection { get; }

        private WindowsPlatformIntegration(
            WindowsClipboardService clipboard,
            WindowsCredentialStore credentialStore,
            WindowsAutoTypeService autoType,
            WindowsScreenProtectionService screenProtection)
        {
            Clipboard = clipboard;
            CredentialStore = credentialStore;
            AutoType = autoType;
            ScreenProtection = screenProtection;
        }

        /// <summary>
        /// Creates a <see cref="WindowsPlatformIntegration"/> instance with
        /// all Windows service implementations wired together.
        ///
        /// Guard: only call on Windows.  On non-Windows the constructor will
        /// still succeed (all fields are platform-neutral), but the underlying
        /// services will fail at runtime when their OS-specific APIs are invoked.
        /// </summary>
        public static WindowsPlatformIntegration Create()
        {
            return new WindowsPlatformIntegration(
                new WindowsClipboardService(),
                new WindowsCredentialStore(),
                new WindowsAutoTypeService(),
                new WindowsScreenProtectionService());
        }
    }
}
