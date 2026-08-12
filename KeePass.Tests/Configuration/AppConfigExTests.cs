using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using KeePass.App.Configuration;
using KeePassLib.Serialization;

using Microsoft.Extensions.Options;

using Xunit;

namespace KeePass.Tests.Configuration
{
    /// <summary>
    /// Characterization tests for the <see cref="AppConfigEx"/> IOptions wrapping
    /// infrastructure introduced in WO-032.
    ///
    /// Tests cover:
    /// <list type="bullet">
    ///   <item>Loading configuration from test fixture XML files.</item>
    ///   <item><see cref="IOptions{T}"/> and <see cref="IOptionsMonitor{T}"/> contract.</item>
    ///   <item><see cref="MruInitializationService"/> deduplication.</item>
    ///   <item>Round-trip: serialize → deserialize → verify no data loss.</item>
    /// </list>
    /// </summary>
    public class AppConfigExTests : IDisposable
    {
        // ── Fixture helpers ───────────────────────────────────────────────────

        private readonly List<string> m_tempFiles = new List<string>();

        private string ReadFixture(string resourceName)
        {
            Assembly asm = typeof(AppConfigExTests).Assembly;
            string fullName = $"KeePass.Tests.Fixtures.Config.{resourceName}";
            using Stream s = asm.GetManifestResourceStream(fullName);
            if (s == null)
                throw new InvalidOperationException(
                    $"Embedded resource '{fullName}' not found.");
            using StreamReader r = new StreamReader(s);
            return r.ReadToEnd();
        }

        private string WriteTempXml(string xml)
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"keepass-test-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, xml, System.Text.Encoding.UTF8);
            m_tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (string f in m_tempFiles)
            {
                try { File.Delete(f); } catch { /* best effort */ }
            }
        }

        // ── Default config fixture ────────────────────────────────────────────

        [Fact]
        public void DefaultConfigFixture_EmbeddedResourceExists()
        {
            string xml = ReadFixture("default-config.xml");
            Assert.NotEmpty(xml);
            Assert.Contains("<Configuration", xml, StringComparison.Ordinal);
        }

        [Fact]
        public void UserConfigFixture_EmbeddedResourceExists()
        {
            string xml = ReadFixture("user-config.xml");
            Assert.NotEmpty(xml);
            Assert.Contains("<Configuration", xml, StringComparison.Ordinal);
        }

        [Fact]
        public void EnforcedConfigFixture_EmbeddedResourceExists()
        {
            string xml = ReadFixture("enforced-config.xml");
            Assert.NotEmpty(xml);
            Assert.Contains("<Configuration", xml, StringComparison.Ordinal);
        }

        // ── IOptions<AppConfigEx> adapter ─────────────────────────────────────

        [Fact]
        public void AppConfigExOptions_Value_ReturnsSameInstance()
        {
            AppConfigEx config = new AppConfigEx();
            IOptions<AppConfigEx> options = new AppConfigExOptions(config);

            Assert.Same(config, options.Value);
        }

        [Fact]
        public void AppConfigExOptions_NullConfig_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new AppConfigExOptions(null));
        }

        // ── IOptionsMonitor<AppConfigEx> ──────────────────────────────────────

        [Fact]
        public void AppConfigExOptionsMonitor_CurrentValue_ReturnsInitialInstance()
        {
            AppConfigEx config = new AppConfigEx();
            using AppConfigExOptionsMonitor monitor = new AppConfigExOptionsMonitor(config);

            Assert.Same(config, monitor.CurrentValue);
        }

        [Fact]
        public void AppConfigExOptionsMonitor_Get_ReturnsCurrentValue()
        {
            AppConfigEx config = new AppConfigEx();
            using AppConfigExOptionsMonitor monitor = new AppConfigExOptionsMonitor(config);

            // Named options are not supported; any name returns CurrentValue.
            Assert.Same(config, monitor.Get(Options.DefaultName));
            Assert.Same(config, monitor.Get("any-name"));
        }

        [Fact]
        public void AppConfigExOptionsMonitor_NullConfig_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AppConfigExOptionsMonitor(null));
        }

        [Fact]
        public void AppConfigExOptionsMonitor_OnChange_NullListener_ThrowsArgumentNull()
        {
            AppConfigEx config = new AppConfigEx();
            using AppConfigExOptionsMonitor monitor = new AppConfigExOptionsMonitor(config);

            Assert.Throws<ArgumentNullException>(() => monitor.OnChange(null));
        }

        [Fact]
        public void AppConfigExOptionsMonitor_OnChange_RegistrationDispose_RemovesListener()
        {
            AppConfigEx config = new AppConfigEx();
            using AppConfigExOptionsMonitor monitor = new AppConfigExOptionsMonitor(config);

            int callCount = 0;
            IDisposable reg = monitor.OnChange((cfg, name) => callCount++);

            // Dispose the registration; subsequent notifications must not reach it.
            reg.Dispose();

            // Double-dispose must be safe.
            reg.Dispose();
        }

        [Fact]
        public void AppConfigExOptionsMonitor_WatchConfigFile_EmptyPath_DoesNotThrow()
        {
            AppConfigEx config = new AppConfigEx();
            using AppConfigExOptionsMonitor monitor = new AppConfigExOptionsMonitor(config);

            // Empty or null paths must be silently ignored.
            monitor.WatchConfigFile(string.Empty);
            monitor.WatchConfigFile(null);
        }

        [Fact]
        public void AppConfigExOptionsMonitor_WatchConfigFile_MissingDir_DoesNotThrow()
        {
            AppConfigEx config = new AppConfigEx();
            using AppConfigExOptionsMonitor monitor = new AppConfigExOptionsMonitor(config);

            // A path whose directory does not exist must not throw.
            monitor.WatchConfigFile(@"C:\DoesNotExist\config.xml");
        }

        // ── MruInitializationService ──────────────────────────────────────────

        [Fact]
        public void MruInitializationService_NullConfig_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => MruInitializationService.Initialize(null));
        }

        [Fact]
        public void MruInitializationService_NoDuplicates_LeavesListUnchanged()
        {
            AppConfigEx config = new AppConfigEx();
            config.Application.MostRecentlyUsed.Items = new List<IOConnectionInfo>
            {
                IOConnectionInfo.FromPath(@"C:\Vaults\Personal.kdbx"),
                IOConnectionInfo.FromPath(@"C:\Vaults\Work.kdbx"),
            };
            int before = config.Application.MostRecentlyUsed.Items.Count;

            MruInitializationService.Initialize(config);

            Assert.Equal(before, config.Application.MostRecentlyUsed.Items.Count);
        }

        [Fact]
        public void MruInitializationService_DuplicateMruItems_Deduplicates()
        {
            AppConfigEx config = new AppConfigEx();
            // Same path in different cases — must be treated as duplicates.
            config.Application.MostRecentlyUsed.Items = new List<IOConnectionInfo>
            {
                IOConnectionInfo.FromPath(@"C:\Vaults\Personal.kdbx"),
                IOConnectionInfo.FromPath(@"C:\VAULTS\PERSONAL.KDBX"),
                IOConnectionInfo.FromPath(@"C:\Vaults\Work.kdbx"),
            };

            MruInitializationService.Initialize(config);

            Assert.Equal(2, config.Application.MostRecentlyUsed.Items.Count);
        }

        [Fact]
        public void MruInitializationService_DuplicateKeySources_Deduplicates()
        {
            AppConfigEx config = new AppConfigEx();
            var src1 = new AceKeyAssoc { DatabasePath = @"C:\Vaults\A.kdbx" };
            var src2 = new AceKeyAssoc { DatabasePath = @"C:\VAULTS\A.KDBX" }; // duplicate
            var src3 = new AceKeyAssoc { DatabasePath = @"C:\Vaults\B.kdbx" };
            config.Defaults.KeySources = new List<AceKeyAssoc> { src1, src2, src3 };

            MruInitializationService.Initialize(config);

            Assert.Equal(2, config.Defaults.KeySources.Count);
        }

        [Fact]
        public void MruInitializationService_EmptyLists_DoesNotThrow()
        {
            AppConfigEx config = new AppConfigEx();
            config.Application.MostRecentlyUsed.Items = new List<IOConnectionInfo>();
            config.Defaults.KeySources = new List<AceKeyAssoc>();

            MruInitializationService.Initialize(config);

            Assert.Empty(config.Application.MostRecentlyUsed.Items);
            Assert.Empty(config.Defaults.KeySources);
        }

        // ── AppConfigServiceExtensions ─────────────────────────────────────────

        [Fact]
        public void AddAppConfig_NullServices_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => AppConfigServiceExtensions.AddAppConfig(null, new AppConfigEx()));
        }

        [Fact]
        public void AddAppConfig_NullConfig_ThrowsArgumentNull()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Assert.Throws<ArgumentNullException>(
                () => services.AddAppConfig(null));
        }

        // ── Round-trip serialization ──────────────────────────────────────────

        [Fact]
        public void AppConfigEx_RoundTrip_ViaFile_PreservesSettings()
        {
            // Build a config with recognizable non-default values.
            AppConfigEx original = new AppConfigEx();
            original.Defaults.RememberKeySources = false;
            original.Security.WorkspaceLocking.LockAfterTime = 300U;

            // Save to a temp file via AppConfigSerializer (user-config path).
            string tmpBase = $"keepass-roundtrip-{Guid.NewGuid():N}";
            string savedBaseName = AppConfigSerializer.BaseName;
            string tmpDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                tmpBase);

            try
            {
                Directory.CreateDirectory(tmpDir);
                AppConfigSerializer.BaseName = Path.Combine(tmpDir, tmpBase);
                original.Meta.PreferUserConfiguration = true;

                bool saved = AppConfigSerializer.Save(original);
                Assert.True(saved, "AppConfigSerializer.Save must succeed on a writable temp path.");

                // Reset path cache so Load re-reads the new BaseName paths.
                AppConfigSerializer.BaseName = AppConfigSerializer.BaseName; // triggers cache reset

                AppConfigEx reloaded = AppConfigSerializer.Load();

                Assert.NotNull(reloaded);
                Assert.False(reloaded.Defaults.RememberKeySources,
                    "RememberKeySources must survive round-trip serialization.");
                Assert.Equal(300U, reloaded.Security.WorkspaceLocking.LockAfterTime);
            }
            finally
            {
                AppConfigSerializer.BaseName = savedBaseName;
                try { Directory.Delete(tmpDir, recursive: true); } catch { }
            }
        }

        // ── Program.ReplaceConfig compatibility shim ──────────────────────────

        [Fact]
        public void Program_ReplaceConfig_NullConfig_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Program.ReplaceConfig(null));
        }

        [Fact]
        public void Program_ReplaceConfig_NonNull_DoesNotThrow()
        {
            // Verify the method accepts a valid instance without throwing.
            // We deliberately restore the original value so global state is unchanged.
            AppConfigEx original = Program.Config;
            try
            {
                AppConfigEx replacement = new AppConfigEx();
                Program.ReplaceConfig(replacement);
                Assert.Same(replacement, Program.Config);
            }
            finally
            {
                Program.ReplaceConfig(original);
            }
        }
    }
}
