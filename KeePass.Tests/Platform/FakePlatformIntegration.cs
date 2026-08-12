/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Collections.Generic;

using KeePass.Core.Platform;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Reusable test double for <see cref="IPlatformIntegration"/> (WO-038).
	///
	/// <para>Unlike <see cref="TestPlatformIntegration"/> (which throws on
	/// unsupported sub-service operations), <c>FakePlatformIntegration</c>
	/// provides silent no-op implementations for all sub-services so that tests
	/// which exercise cross-cutting logic do not need to configure every member.</para>
	///
	/// <para>Usage:</para>
	/// <code>
	/// var fake = new FakePlatformIntegration(PlatformId.Linux)
	/// {
	///     CapabilityOverrides =
	///     {
	///         [PlatformCapability.Clipboard] = PlatformCapabilityTier.Partial
	///     }
	/// };
	/// </code>
	/// </summary>
	public sealed class FakePlatformIntegration : IPlatformIntegration
	{
		// ── Configuration ──────────────────────────────────────────────────────

		/// <summary>
		/// Override the tier returned for specific capabilities.  Capabilities
		/// not present in this dictionary fall through to the platform defaults
		/// defined in <see cref="GetCapabilityTier"/>.
		/// </summary>
		public Dictionary<PlatformCapability, PlatformCapabilityTier> CapabilityOverrides { get; } =
			new Dictionary<PlatformCapability, PlatformCapabilityTier>();

		// ── IPlatformIntegration ───────────────────────────────────────────────

		/// <inheritdoc/>
		public PlatformId PlatformId { get; }

		/// <inheritdoc/>
		public bool SupportsAlwaysOnTop { get; set; }

		/// <inheritdoc/>
		public bool RequiresWindowMinSizeEnforcement { get; set; }

		/// <inheritdoc/>
		public IClipboardService Clipboard { get; set; }

		/// <inheritdoc/>
		public ICredentialStore CredentialStore { get; set; }

		/// <inheritdoc/>
		public IAutoTypeService AutoType { get; set; }

		/// <inheritdoc/>
		public IScreenProtectionService ScreenProtection { get; set; }

		/// <inheritdoc/>
		public PlatformCapabilityTier GetCapabilityTier(PlatformCapability capability)
		{
			// Check explicit overrides first.
			PlatformCapabilityTier tier;
			if(CapabilityOverrides.TryGetValue(capability, out tier))
				return tier;

			// Platform-appropriate defaults.
			return DefaultTierFor(PlatformId, capability);
		}

		// ── Ctor ───────────────────────────────────────────────────────────────

		/// <summary>
		/// Creates a <see cref="FakePlatformIntegration"/> for the specified
		/// platform with silent no-op sub-services.
		/// </summary>
		/// <param name="platformId">
		/// The platform ID to report.  Defaults to
		/// <see cref="PlatformId.Windows"/>.
		/// </param>
		public FakePlatformIntegration(PlatformId platformId = PlatformId.Windows)
		{
			PlatformId = platformId;
			SupportsAlwaysOnTop = (platformId != PlatformId.Linux);
			RequiresWindowMinSizeEnforcement = (platformId == PlatformId.Linux);

			Clipboard = new NoOpClipboardService();
			CredentialStore = new NoOpCredentialStore();
			AutoType = new NullAutoTypeService();
			ScreenProtection = new NullScreenProtectionService();
		}

		// ── Static helper ──────────────────────────────────────────────────────

		/// <summary>
		/// Returns the sensible default capability tier for a given platform/
		/// capability pair — the same defaults used by the real implementations.
		/// </summary>
		public static PlatformCapabilityTier DefaultTierFor(
			PlatformId platformId, PlatformCapability capability)
		{
			switch(platformId)
			{
				case PlatformId.Windows:
					switch(capability)
					{
						case PlatformCapability.Clipboard:               return PlatformCapabilityTier.Full;
						case PlatformCapability.ClipboardPrivacyMarkers: return PlatformCapabilityTier.Full;
						case PlatformCapability.CredentialStore:         return PlatformCapabilityTier.Full;
						case PlatformCapability.AutoType:                return PlatformCapabilityTier.Full;
						case PlatformCapability.SecureDesktop:           return PlatformCapabilityTier.Full;
						case PlatformCapability.ScreenCaptureProtection: return PlatformCapabilityTier.Full;
						case PlatformCapability.ProcessDacl:             return PlatformCapabilityTier.Full;
						case PlatformCapability.GlobalHotKeys:           return PlatformCapabilityTier.Full;
						default:                                         return PlatformCapabilityTier.Unsupported;
					}

				case PlatformId.MacOS:
					switch(capability)
					{
						case PlatformCapability.Clipboard:       return PlatformCapabilityTier.Full;
						case PlatformCapability.CredentialStore: return PlatformCapabilityTier.Full;
						default:                                 return PlatformCapabilityTier.Unsupported;
					}

				case PlatformId.Linux:
					// Defaults represent a standard X11 Linux desktop with secret-tool available.
					// Wayland-specific tiers (Clipboard=Partial, ClipboardPrivacyMarkers=Partial)
					// must be tested via CapabilityOverrides.
					switch(capability)
					{
						case PlatformCapability.Clipboard:       return PlatformCapabilityTier.Full;
						case PlatformCapability.CredentialStore: return PlatformCapabilityTier.Full;
						default:                                 return PlatformCapabilityTier.Unsupported;
					}

				default:
					return PlatformCapabilityTier.Unsupported;
			}
		}

		// ── No-op sub-services ─────────────────────────────────────────────────

		private sealed class NoOpClipboardService : IClipboardService
		{
			private string _text;

			public bool IsSupported => true;

			public void SetText(string text) { _text = text; }
			public string GetText() { return _text ?? string.Empty; }
			public void Clear() { _text = null; }
			public void ClearIfOwner() { _text = null; }
			public void SetWithAutoClear(string text, TimeSpan timeout) { _text = text; }
		}

		private sealed class NoOpCredentialStore : ICredentialStore
		{
			private readonly Dictionary<string, byte[]> _store =
				new Dictionary<string, byte[]>();

			public bool IsSupported => true;

			public void Store(string key, byte[] secret)
			{
				if(key != null) _store[key] = secret;
			}

			public byte[] Retrieve(string key)
			{
				if(key == null) return null;
				byte[] v;
				return _store.TryGetValue(key, out v) ? v : null;
			}

			public void Delete(string key)
			{
				if(key != null) _store.Remove(key);
			}
		}
	}
}
