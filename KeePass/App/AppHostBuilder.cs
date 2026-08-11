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

using System;
using System.Runtime.InteropServices;

using KeePass.App.Configuration;
using KeePass.Core.Platform;
using KeePass.Core.Services;
using KeePass.DataExchange;
using KeePass.Platform;
using KeePass.Platform.Unix.Linux;
using KeePass.Platform.Unix.Mac;
using KeePass.Services;

using KeePassLib.Cryptography.Cipher;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeePass.App
{
	/// <summary>
	/// Composition root for the KeePass application.  Configures a
	/// <see cref="ServiceCollection"/> with all core services, then builds
	/// and exposes the resulting <see cref="IServiceProvider"/>.
	///
	/// Usage in <c>Program.Main</c>:
	/// <code>
	///   AppHostBuilder host = new AppHostBuilder(g_appConfig, g_fmtPool);
	///   host.Build();
	///   Program.Services = host.Services;
	/// </code>
	/// </summary>
	public sealed class AppHostBuilder
	{
		private readonly AppConfigEx m_config;
		private readonly FileFormatPool m_fmtPool;
		private IServiceProvider m_services;

		/// <summary>
		/// The built <see cref="IServiceProvider"/>.  <c>null</c> before
		/// <see cref="Build"/> is called.
		/// </summary>
		public IServiceProvider Services => m_services;

		/// <param name="config">
		/// The loaded application configuration; must not be <c>null</c>.
		/// </param>
		/// <param name="fileFormatPool">
		/// The pool of registered import/export file formats; must not be <c>null</c>.
		/// </param>
		public AppHostBuilder(AppConfigEx config, FileFormatPool fileFormatPool)
		{
			if(config == null) throw new ArgumentNullException("config");
			if(fileFormatPool == null) throw new ArgumentNullException("fileFormatPool");

			m_config = config;
			m_fmtPool = fileFormatPool;
		}

		/// <summary>
		/// Configures services and builds the <see cref="IServiceProvider"/>.
		/// Idempotent — calling <see cref="Build"/> more than once rebuilds with
		/// the same registrations (useful for tests that need a fresh container).
		/// </summary>
		public IServiceProvider Build()
		{
			ServiceCollection services = new ServiceCollection();
			ConfigureServices(services);
			m_services = services.BuildServiceProvider();
			return m_services;
		}

		/// <summary>
		/// Adds all KeePass service registrations to the supplied collection.
		/// Split from <see cref="Build"/> to allow tests to inspect or augment
		/// registrations before building the provider.
		/// </summary>
		public void ConfigureServices(IServiceCollection services)
		{
			if(services == null) throw new ArgumentNullException("services");

			// ── Structured logging (WO-031) ───────────────────────────────────
			services.AddLogging(builder =>
			{
				// Default provider; hosts can replace this with AddConsole,
				// AddEventLog, etc. by reconfiguring the factory after Build().
				builder.SetMinimumLevel(LogLevel.Information);
			});

			// ── Application configuration / IOptions (WO-032) ────────────────
			services.AddAppConfig(m_config);

			// ── Platform integration (WO-026, WO-028, WO-029) ────────────────
			RegisterPlatformIntegration(services);

		// ── UI services (WO-030) ──────────────────────────────────────────
		services.AddSingleton<IMessageService, WinFormsMessageService>();
		services.AddSingleton<IDialogService, WinFormsDialogService>();

		// ── UI command service (WO-037) ───────────────────────────────────
		// IUICommandService abstracts Program.MainForm calls from the Ecas
		// trigger system and other application-layer services.
		services.AddSingleton<IUICommandService, WinFormsUICommandService>();

			// ── Image service ─────────────────────────────────────────────────
			// NullImageService until a Windows-native implementation is added.
			services.AddSingleton<IImageService, NullImageService>();

			// ── Crypto / format pools ─────────────────────────────────────────
			// CipherPool: registered as the single global instance so all
			// consumers see the same set of registered cipher engines.
			services.AddSingleton(CipherPool.GlobalPool);

			// KdfPool is a static class whose Engines collection is initialized
			// lazily.  Register a factory delegate so consumers that accept
			// IEnumerable<KdfEngine> are wired automatically.
			services.AddSingleton(KeePassLib.Cryptography.KeyDerivation.KdfPool.Engines);

			// FileFormatPool: singleton that was populated during app startup.
			services.AddSingleton(m_fmtPool);
		}

		// ── Platform registration helper ──────────────────────────────────────

		private static void RegisterPlatformIntegration(IServiceCollection services)
		{
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				services.AddSingleton<IPlatformIntegration, WindowsPlatformIntegration>();
			}
			else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				services.AddSingleton<IPlatformIntegration, MacPlatformIntegration>();
			}
			else
			{
				// Linux and all other Unix-like systems.
				services.AddSingleton<IPlatformIntegration, LinuxPlatformIntegration>();
			}
		}
	}
}
