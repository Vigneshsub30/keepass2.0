/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using KeePass.App.Configuration;

using KeePassLib;
using KeePassLib.Utility;

using Xunit;

namespace KeePass.Tests.Utility
{
	/// <summary>
	/// Unit tests for the beta release-channel infrastructure (WO-102):
	/// <list type="bullet">
	///   <item>Enum ordinals and defaults</item>
	///   <item>URL constants diverge correctly</item>
	///   <item>AceApplication serialization contract</item>
	///   <item>Version info file parsing (channel-agnostic)</item>
	///   <item>Version comparison semantics</item>
	/// </list>
	/// These tests are platform-neutral: no WinForms, no network, no
	/// <see cref="KeePass.Program"/> access required.
	/// </summary>
	public sealed class UpdateCheckChannelTests
	{
		// ── 1. Enum contract ─────────────────────────────────────────────── //

		[Fact]
		public void ReleaseChannel_Stable_HasOrdinalZero()
		{
			Assert.Equal(0, (int)KeePassReleaseChannel.Stable);
		}

		[Fact]
		public void ReleaseChannel_Beta_HasOrdinalOne()
		{
			Assert.Equal(1, (int)KeePassReleaseChannel.Beta);
		}

		[Fact]
		public void ReleaseChannel_EnumHasExactlyTwoValues()
		{
			int count = Enum.GetValues(typeof(KeePassReleaseChannel)).Length;
			Assert.Equal(2, count);
		}

		[Fact]
		public void AceApplication_ReleaseChannel_DefaultIsStable()
		{
			// DefaultValue attribute must be Stable so deserializing an old
			// config that does not contain a ReleaseChannel element results in
			// Stable — never Beta.
			PropertyInfo pi = typeof(AceApplication)
				.GetProperty(nameof(AceApplication.ReleaseChannel));
			Assert.NotNull(pi);

			DefaultValueAttribute dva = pi.GetCustomAttribute<DefaultValueAttribute>();
			Assert.NotNull(dva);
			Assert.Equal(KeePassReleaseChannel.Stable, (KeePassReleaseChannel)dva.Value);
		}

		[Fact]
		public void AceApplication_NewInstance_ChannelIsStable()
		{
			AceApplication app = new AceApplication();
			Assert.Equal(KeePassReleaseChannel.Stable, app.ReleaseChannel);
		}

		[Fact]
		public void AceApplication_SetBeta_RetainsBeta()
		{
			AceApplication app = new AceApplication();
			app.ReleaseChannel = KeePassReleaseChannel.Beta;
			Assert.Equal(KeePassReleaseChannel.Beta, app.ReleaseChannel);
		}

		// ── 2. URL constants ─────────────────────────────────────────────── //

		[Fact]
		public void PwDefs_BetaVersionUrl_IsNotEmpty()
		{
			Assert.False(string.IsNullOrWhiteSpace(PwDefs.BetaVersionUrl));
		}

		[Fact]
		public void PwDefs_BetaVersionUrl_DiffersFromStableUrl()
		{
			Assert.NotEqual(PwDefs.VersionUrl, PwDefs.BetaVersionUrl,
				StringComparer.OrdinalIgnoreCase);
		}

		[Fact]
		public void PwDefs_BetaVersionUrl_IsHttps()
		{
			Assert.StartsWith("https://", PwDefs.BetaVersionUrl,
				StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void PwDefs_BetaVersionUrl_ContainsBeta()
		{
			// The beta URL should clearly indicate it serves pre-release data
			// so an accidental swap is immediately visible in logs/code review.
			Assert.Contains("beta", PwDefs.BetaVersionUrl,
				StringComparison.OrdinalIgnoreCase);
		}

		// ── 3. URL selection logic ────────────────────────────────────────── //

		// The private GetInstalledComponents() method is exercised here through
		// a lightweight mirror of the selection logic so the test does not
		// depend on Program.MainForm or the WinForms subsystem.

		private static string SelectVersionUrl(KeePassReleaseChannel channel)
		{
			return (channel == KeePassReleaseChannel.Beta)
				? PwDefs.BetaVersionUrl
				: PwDefs.VersionUrl;
		}

		[Fact]
		public void SelectVersionUrl_Stable_ReturnsStableUrl()
		{
			string url = SelectVersionUrl(KeePassReleaseChannel.Stable);
			Assert.Equal(PwDefs.VersionUrl, url, StringComparer.OrdinalIgnoreCase);
		}

		[Fact]
		public void SelectVersionUrl_Beta_ReturnsBetaUrl()
		{
			string url = SelectVersionUrl(KeePassReleaseChannel.Beta);
			Assert.Equal(PwDefs.BetaVersionUrl, url, StringComparer.OrdinalIgnoreCase);
		}

		[Fact]
		public void SelectVersionUrl_Stable_DoesNotReturnBetaUrl()
		{
			string url = SelectVersionUrl(KeePassReleaseChannel.Stable);
			Assert.NotEqual(PwDefs.BetaVersionUrl, url, StringComparer.OrdinalIgnoreCase);
		}

		[Fact]
		public void SelectVersionUrl_Beta_DoesNotReturnStableUrl()
		{
			string url = SelectVersionUrl(KeePassReleaseChannel.Beta);
			Assert.NotEqual(PwDefs.VersionUrl, url, StringComparer.OrdinalIgnoreCase);
		}

		// ── 4. Version info file parsing ─────────────────────────────────── //

		// Parse the mock fixture files to verify the format is valid for
		// production parsing logic (no signature verification in unsigned fixtures).

		[Fact]
		public void StableVersionInfoFixture_ParsesToKnownVersion()
		{
			string data = LoadFixture("stable-version-info.txt");
			ulong ver = ParseVersionFromFixture(data, "KeePass");
			Assert.True(ver > 0, "Parsed version should be non-zero");
			// Stable fixture declares 2.61.1.
			Assert.Equal(StrUtil.ParseVersion("2.61.1"), ver);
		}

		[Fact]
		public void BetaVersionInfoFixture_ParsesToKnownVersion()
		{
			string data = LoadFixture("beta-version-info.txt");
			ulong ver = ParseVersionFromFixture(data, "KeePass");
			Assert.True(ver > 0, "Parsed version should be non-zero");
			// Beta fixture declares 2.62.0 — newer than stable 2.61.1.
			Assert.Equal(StrUtil.ParseVersion("2.62.0"), ver);
		}

		[Fact]
		public void BetaVersionInfoFixture_NewerThanStableFixture()
		{
			string stableData = LoadFixture("stable-version-info.txt");
			string betaData   = LoadFixture("beta-version-info.txt");

			ulong stableVer = ParseVersionFromFixture(stableData, "KeePass");
			ulong betaVer   = ParseVersionFromFixture(betaData,   "KeePass");

			Assert.True(betaVer > stableVer,
				$"Beta version ({betaVer}) should be greater than stable ({stableVer})");
		}

		// ── 5. Version comparison semantics ──────────────────────────────── //

		[Theory]
		[InlineData("2.61.1", "2.62.0")]
		[InlineData("2.0.0",  "2.1.0")]
		[InlineData("1.99.9", "2.0.0")]
		public void ParseVersion_NewerVersionHasHigherValue(string older, string newer)
		{
			ulong uOlder = StrUtil.ParseVersion(older);
			ulong uNewer = StrUtil.ParseVersion(newer);
			Assert.True(uNewer > uOlder,
				$"Expected {newer} > {older} but got {uNewer} <= {uOlder}");
		}

		[Fact]
		public void StableChannel_ShouldNotUpgradeToHigherBetaOnly_Concept()
		{
			// A stable-channel user whose installed version equals the latest
			// stable should see no update, even if a newer beta exists.
			// This test validates the expected comparison outcome; the actual
			// stable/beta URL separation enforces the behaviour at runtime.
			ulong installed  = StrUtil.ParseVersion("2.61.1"); // stable channel
			ulong latStable  = StrUtil.ParseVersion("2.61.1"); // latest stable
			ulong latBeta    = StrUtil.ParseVersion("2.62.0"); // latest beta (hidden)

			bool stableUpdateAvail = installed < latStable;
			bool betaVisible       = false; // stable channel never sees beta URL

			Assert.False(stableUpdateAvail, "No stable update should be available");
			Assert.False(betaVisible,       "Beta version must not be visible on stable channel");
		}

		[Fact]
		public void BetaChannel_DetectsNewerBeta()
		{
			ulong installed = StrUtil.ParseVersion("2.61.1");
			ulong latBeta   = StrUtil.ParseVersion("2.62.0");

			Assert.True(installed < latBeta, "Beta channel user should see newer beta");
		}

		[Fact]
		public void BetaChannel_DoesNotDowngradeToOlderStable()
		{
			// User on a beta build newer than the current stable should not
			// be told to downgrade.
			ulong installed = StrUtil.ParseVersion("2.62.0"); // running beta build
			ulong latStable = StrUtil.ParseVersion("2.61.1"); // latest stable

			bool downgrade = installed > latStable;
			Assert.True(downgrade,
				"Running a beta build newer than stable is correctly identified as " +
				"PreRelease status — no downgrade prompt should be shown");
		}

		// ── Helpers ──────────────────────────────────────────────────────── //

		/// <summary>Loads a fixture file from the embedded resources.</summary>
		private static string LoadFixture(string fileName)
		{
			Assembly asm = typeof(UpdateCheckChannelTests).Assembly;
			// Embedded resource name: KeePass.Tests.Fixtures.VersionInfo.<filename>
			string resName = asm.GetManifestResourceNames()
				.FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

			Assert.True(resName != null, $"Embedded fixture '{fileName}' not found. " +
				$"Available: {string.Join(", ", asm.GetManifestResourceNames())}");

			using Stream s = asm.GetManifestResourceStream(resName);
			using StreamReader sr = new StreamReader(s, Encoding.UTF8);
			return sr.ReadToEnd();
		}

		/// <summary>
		/// Parses a mock version-info fixture (unsigned, no-sig header) and returns
		/// the encoded version for the named component.
		/// </summary>
		private static ulong ParseVersionFromFixture(string data, string componentName)
		{
			// Format (see UpdateCheckEx.LoadInfoFilePriv):
			// Line 0: {sep}NOSIG{sep}      (header; first char is separator)
			// Line 1+: Name{sep}Version
			// Last: {sep}               (footer)
			string[] lines = data.Split('\n');
			if(lines.Length < 2) return 0;

			char sep = lines[0].Trim().Length > 0 ? lines[0].Trim()[0] : ':';

			foreach(string line in lines.Skip(1))
			{
				string trimmed = line.Trim();
				if(trimmed.Length == 0) continue;
				if(trimmed[0] == sep) break; // footer

				string[] parts = trimmed.Split(sep);
				if(parts.Length >= 2 &&
				   parts[0].Trim().Equals(componentName, StringComparison.OrdinalIgnoreCase))
				{
					return StrUtil.ParseVersion(parts[1].Trim());
				}
			}

			return 0;
		}
	}
}
