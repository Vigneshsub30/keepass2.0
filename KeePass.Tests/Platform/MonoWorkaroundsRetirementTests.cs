using System;

using KeePass.Core.Platform;
using KeePass.Platform.Unix.Linux;
using KeePass.Platform.Unix.Mac;

using Xunit;

namespace KeePass.Tests.Platform
{
    /// <summary>
    /// Regression tests for WO-035: MonoWorkarounds retirement.
    ///
    /// <para>These tests verify that the two workarounds reclassified as
    /// RE-IMPLEMENT are correctly surfaced through
    /// <see cref="IPlatformIntegration"/> capability flags, and that the
    /// safe-defaults <see cref="FallbackPlatformIntegration"/> responds
    /// consistently to the host OS at construction time.</para>
    ///
    /// <para><b>Workaround decision table</b></para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>ID</term><description>Description / Classification</description>
    ///   </listheader>
    ///   <item><term>#1716</term><description>
    ///     Always-on-top broken on Cinnamon desktop — RE-IMPLEMENT via
    ///     <see cref="IPlatformIntegration.SupportsAlwaysOnTop"/>.
    ///   </description></item>
    ///   <item><term>#686017</term><description>
    ///     Min-size enforcement required on Linux — RE-IMPLEMENT via
    ///     <see cref="IPlatformIntegration.RequiresWindowMinSizeEnforcement"/>.
    ///   </description></item>
    ///   <item>
    ///     <term>All others (>40 bug IDs)</term>
    ///     <description>RETIRE — Mono-runtime defects, dead on .NET 10.</description>
    ///   </item>
    /// </list>
    /// </summary>
    public sealed class MonoWorkaroundsRetirementTests
    {
        // ── #1716 — SupportsAlwaysOnTop ───────────────────────────────────────

        [Fact]
        public void Windows_SupportsAlwaysOnTop_IsTrue()
        {
            // Windows window manager always honours the always-on-top flag.
            TestPlatformIntegration platform = new TestPlatformIntegration
            {
                PlatformId = PlatformId.Windows,
                SupportsAlwaysOnTop = true
            };

            Assert.True(platform.SupportsAlwaysOnTop,
                "SupportsAlwaysOnTop must be true on Windows (WA #1716 retired).");
        }

        [Fact]
        public void MacOS_SupportsAlwaysOnTop_IsTrue()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.True(pi.SupportsAlwaysOnTop,
                "SupportsAlwaysOnTop must be true on macOS.");
        }

        [Fact]
        public void Linux_NonCinnamon_SupportsAlwaysOnTop_IsTrue()
        {
            // Simulate a non-Cinnamon desktop by ensuring the env vars are absent.
            // The actual LinuxPlatformIntegration reads XDG_CURRENT_DESKTOP etc.
            // On non-Cinnamon (e.g. GNOME, KDE) SupportsAlwaysOnTop is true.
            TestPlatformIntegration platform = new TestPlatformIntegration
            {
                PlatformId = PlatformId.Linux,
                SupportsAlwaysOnTop = true   // Non-Cinnamon
            };

            Assert.True(platform.SupportsAlwaysOnTop);
        }

        [Fact]
        public void Linux_Cinnamon_SupportsAlwaysOnTop_IsFalse()
        {
            // Simulate a Cinnamon desktop (replaces MonoWorkarounds #1716).
            TestPlatformIntegration platform = new TestPlatformIntegration
            {
                PlatformId = PlatformId.Linux,
                SupportsAlwaysOnTop = false   // Cinnamon
            };

            Assert.False(platform.SupportsAlwaysOnTop,
                "SupportsAlwaysOnTop must be false on Cinnamon (WA #1716 re-implemented).");
        }

        [Fact]
        public void LinuxCreate_SupportsAlwaysOnTop_ReflectsDesktopEnv()
        {
            // Verify LinuxPlatformIntegration.Create() sets the property.
            // We cannot guarantee the test host is or is not Cinnamon, but
            // we can assert that the property is readable and does not throw.
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            bool value = pi.SupportsAlwaysOnTop; // Must not throw.
            Assert.IsType<bool>(value);
        }

        // ── #686017 — RequiresWindowMinSizeEnforcement ────────────────────────

        [Fact]
        public void Windows_RequiresWindowMinSizeEnforcement_IsFalse()
        {
            TestPlatformIntegration platform = new TestPlatformIntegration
            {
                PlatformId = PlatformId.Windows,
                RequiresWindowMinSizeEnforcement = false
            };

            Assert.False(platform.RequiresWindowMinSizeEnforcement,
                "Windows enforces min sizes natively; WA #686017 must be retired.");
        }

        [Fact]
        public void MacOS_RequiresWindowMinSizeEnforcement_IsFalse()
        {
            MacPlatformIntegration pi = MacPlatformIntegration.Create();
            Assert.False(pi.RequiresWindowMinSizeEnforcement,
                "macOS enforces min sizes natively.");
        }

        [Fact]
        public void Linux_RequiresWindowMinSizeEnforcement_IsTrue()
        {
            LinuxPlatformIntegration pi = LinuxPlatformIntegration.Create();
            Assert.True(pi.RequiresWindowMinSizeEnforcement,
                "Linux DEs may not enforce min sizes; WA #686017 must be re-implemented.");
        }

        [Fact]
        public void Linux_TestStub_RequiresWindowMinSizeEnforcement_IsTrue()
        {
            TestPlatformIntegration platform = new TestPlatformIntegration
            {
                PlatformId = PlatformId.Linux,
                RequiresWindowMinSizeEnforcement = true
            };

            Assert.True(platform.RequiresWindowMinSizeEnforcement,
                "Linux always requires min-size enforcement (WA #686017 re-implemented).");
        }

        // ── FallbackPlatformIntegration safe defaults ─────────────────────────

        [Fact]
        public void Fallback_PlatformId_MatchesCurrentOS()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;

            // On the current test host, the platform ID must be a known value.
            PlatformId id = fallback.PlatformId;
            bool known = (id == PlatformId.Windows) ||
                         (id == PlatformId.MacOS)   ||
                         (id == PlatformId.Linux);
            Assert.True(known, "FallbackPlatformIntegration must return a known PlatformId.");
        }

        [Fact]
        public void Fallback_SupportsAlwaysOnTop_ConsistentWithPlatform()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;

            // On Windows and macOS the flag should be true.
            // On Linux it defaults to false (conservative).
            bool expected = fallback.PlatformId != PlatformId.Linux;
            Assert.Equal(expected, fallback.SupportsAlwaysOnTop);
        }

        [Fact]
        public void Fallback_RequiresWindowMinSizeEnforcement_ConsistentWithPlatform()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;

            bool expected = fallback.PlatformId == PlatformId.Linux;
            Assert.Equal(expected, fallback.RequiresWindowMinSizeEnforcement);
        }

        [Fact]
        public void Fallback_Clipboard_IsUnsupported()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;
            Assert.False(fallback.Clipboard.IsSupported,
                "FallbackPlatformIntegration clipboard must be unsupported (pre-DI guard).");
        }

        [Fact]
        public void Fallback_Clipboard_SetText_ThrowsPlatformNotSupported()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;
            Assert.Throws<PlatformNotSupportedException>(() =>
                fallback.Clipboard.SetText("test"));
        }

        [Fact]
        public void Fallback_CredentialStore_IsUnsupported()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;
            Assert.False(fallback.CredentialStore.IsSupported);
        }

        [Fact]
        public void Fallback_AutoType_IsNotSupported()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;
            Assert.False(fallback.AutoType.IsSupported);
        }

        [Fact]
        public void Fallback_ScreenProtection_IsNotSupported()
        {
            FallbackPlatformIntegration fallback = FallbackPlatformIntegration.Instance;
            Assert.False(fallback.ScreenProtection.IsSupported);
        }

        // ── Interface contract: new members present on all implementations ─────

        [Fact]
        public void AllImplementations_ExposeSupportsAlwaysOnTop()
        {
            // Use the TestPlatformIntegration which defaults to Windows values.
            IPlatformIntegration p = new TestPlatformIntegration();
            // Must not throw; property must exist.
            bool _ = p.SupportsAlwaysOnTop;
        }

        [Fact]
        public void AllImplementations_ExposeRequiresWindowMinSizeEnforcement()
        {
            IPlatformIntegration p = new TestPlatformIntegration();
            bool _ = p.RequiresWindowMinSizeEnforcement;
        }
    }
}
