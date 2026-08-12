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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeePass.App.Configuration
{
	/// <summary>
	/// Extension methods for registering <see cref="AppConfigEx"/> in a
	/// <see cref="IServiceCollection"/>.
	/// </summary>
	public static class AppConfigServiceExtensions
	{
		/// <summary>
		/// Registers <see cref="AppConfigEx"/> in the DI container as:
		/// <list type="bullet">
		///   <item><see cref="IOptions{T}"/> — always returns the same loaded instance.</item>
		///   <item><see cref="IOptionsMonitor{T}"/> — raises change notifications
		///     when the user-configuration file is modified on disk.</item>
		/// </list>
		/// The singleton <see cref="AppConfigExOptionsMonitor"/> is also registered so
		/// callers that need to manage its lifetime (start/stop file-watching) can
		/// resolve it directly.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The already-loaded configuration instance.</param>
		/// <returns>The same <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection AddAppConfig(
			this IServiceCollection services, AppConfigEx config)
		{
			if(services == null) throw new ArgumentNullException("services");
			if(config == null) throw new ArgumentNullException("config");

			AppConfigExOptionsMonitor monitor = new AppConfigExOptionsMonitor(config);

			// Begin watching for file changes so reloads happen automatically.
			monitor.WatchConfigFile(AppConfigSerializer.UserConfigFile);

			services.AddSingleton<IOptions<AppConfigEx>>(new AppConfigExOptions(config));
			services.AddSingleton<IOptionsMonitor<AppConfigEx>>(monitor);
			services.AddSingleton(monitor); // also resolvable as the concrete type

			return services;
		}
	}
}
