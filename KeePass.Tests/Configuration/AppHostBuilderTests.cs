using System;
using System.Collections.Generic;

using KeePass.App;
using KeePass.App.Configuration;
using KeePass.Core.Platform;
using KeePass.Core.Services;
using KeePass.DataExchange;

using KeePassLib.Cryptography.Cipher;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Xunit;

namespace KeePass.Tests.Configuration
{
    /// <summary>
    /// Unit tests for <see cref="AppHostBuilder"/>, verifying that the DI
    /// container built by <see cref="AppHostBuilder.Build"/> correctly resolves
    /// all registered services without error.
    /// </summary>
    public class AppHostBuilderTests : IDisposable
    {
        private readonly AppConfigEx m_config = new AppConfigEx();
        private readonly FileFormatPool m_pool = new FileFormatPool();
        private IServiceProvider m_sp;

        private IServiceProvider BuildProvider()
        {
            if (m_sp == null)
            {
                AppHostBuilder host = new AppHostBuilder(m_config, m_pool);
                m_sp = host.Build();
            }
            return m_sp;
        }

        public void Dispose()
        {
            (m_sp as IDisposable)?.Dispose();
        }

        // ── Construction guard ────────────────────────────────────────────────

        [Fact]
        public void AppHostBuilder_NullConfig_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AppHostBuilder(null, m_pool));
        }

        [Fact]
        public void AppHostBuilder_NullPool_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AppHostBuilder(m_config, null));
        }

        [Fact]
        public void AppHostBuilder_ConfigureServices_NullServices_ThrowsArgumentNull()
        {
            AppHostBuilder host = new AppHostBuilder(m_config, m_pool);
            Assert.Throws<ArgumentNullException>(() => host.ConfigureServices(null));
        }

        // ── Service resolution ────────────────────────────────────────────────

        [Fact]
        public void IOptions_AppConfigEx_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            IOptions<AppConfigEx> options = sp.GetRequiredService<IOptions<AppConfigEx>>();
            Assert.NotNull(options.Value);
        }

        [Fact]
        public void IOptionsMonitor_AppConfigEx_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            IOptionsMonitor<AppConfigEx> monitor =
                sp.GetRequiredService<IOptionsMonitor<AppConfigEx>>();
            Assert.NotNull(monitor.CurrentValue);
        }

        [Fact]
        public void IOptions_ReturnsRegisteredConfigInstance()
        {
            IServiceProvider sp = BuildProvider();
            IOptions<AppConfigEx> options = sp.GetRequiredService<IOptions<AppConfigEx>>();
            Assert.Same(m_config, options.Value);
        }

        [Fact]
        public void ILoggerFactory_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            ILoggerFactory factory = sp.GetRequiredService<ILoggerFactory>();
            Assert.NotNull(factory);
        }

        [Fact]
        public void ILogger_Generic_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            ILogger<AppHostBuilderTests> logger =
                sp.GetRequiredService<ILogger<AppHostBuilderTests>>();
            Assert.NotNull(logger);
        }

        [Fact]
        public void IPlatformIntegration_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            IPlatformIntegration platform =
                sp.GetRequiredService<IPlatformIntegration>();
            Assert.NotNull(platform);
        }

        [Fact]
        public void IMessageService_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            IMessageService svc = sp.GetRequiredService<IMessageService>();
            Assert.NotNull(svc);
        }

        [Fact]
        public void IDialogService_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            IDialogService svc = sp.GetRequiredService<IDialogService>();
            Assert.NotNull(svc);
        }

        [Fact]
        public void IImageService_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            IImageService svc = sp.GetRequiredService<IImageService>();
            Assert.NotNull(svc);
        }

        [Fact]
        public void CipherPool_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            CipherPool pool = sp.GetRequiredService<CipherPool>();
            Assert.NotNull(pool);
        }

        [Fact]
        public void CipherPool_IsSameAsGlobalPool()
        {
            IServiceProvider sp = BuildProvider();
            CipherPool pool = sp.GetRequiredService<CipherPool>();
            Assert.Same(CipherPool.GlobalPool, pool);
        }

        [Fact]
        public void FileFormatPool_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            FileFormatPool pool = sp.GetRequiredService<FileFormatPool>();
            Assert.NotNull(pool);
        }

        [Fact]
        public void FileFormatPool_IsSameInstancePassedToBuilder()
        {
            IServiceProvider sp = BuildProvider();
            FileFormatPool pool = sp.GetRequiredService<FileFormatPool>();
            Assert.Same(m_pool, pool);
        }

        [Fact]
        public void KdfEngines_ResolvesSuccessfully()
        {
            IServiceProvider sp = BuildProvider();
            IEnumerable<KeePassLib.Cryptography.KeyDerivation.KdfEngine> engines =
                sp.GetRequiredService<IEnumerable<KeePassLib.Cryptography.KeyDerivation.KdfEngine>>();
            Assert.NotNull(engines);
        }

        // ── Program.SetServices shim ──────────────────────────────────────────

        [Fact]
        public void Program_SetServices_NullSp_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Program.SetServices(null));
        }

        [Fact]
        public void Program_Services_AfterSetServices_ReturnsProvider()
        {
            IServiceProvider original = Program.Services;
            IServiceProvider replacement = BuildProvider();
            try
            {
                Program.SetServices(replacement);
                Assert.Same(replacement, Program.Services);
            }
            finally
            {
                // Restore original (may be null; Program.Services only has a getter,
                // so we reach the backing field via the internal setter pattern).
                if (original != null)
                    Program.SetServices(original);
            }
        }

        // ── Build is idempotent ───────────────────────────────────────────────

        [Fact]
        public void Build_CalledTwice_ReturnsNewProvider()
        {
            AppHostBuilder host = new AppHostBuilder(m_config, m_pool);
            IServiceProvider sp1 = host.Build();
            IServiceProvider sp2 = host.Build();

            // Each Build() produces a new ServiceProvider.
            Assert.NotSame(sp1, sp2);

            // But both resolve the same config instance.
            IOptions<AppConfigEx> o1 = sp1.GetRequiredService<IOptions<AppConfigEx>>();
            IOptions<AppConfigEx> o2 = sp2.GetRequiredService<IOptions<AppConfigEx>>();
            Assert.Same(o1.Value, o2.Value);

            (sp1 as IDisposable)?.Dispose();
            (sp2 as IDisposable)?.Dispose();
        }
    }
}
