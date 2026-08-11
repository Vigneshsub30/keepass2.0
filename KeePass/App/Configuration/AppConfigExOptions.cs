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

using Microsoft.Extensions.Options;

namespace KeePass.App.Configuration
{
	/// <summary>
	/// <see cref="IOptions{T}"/> adapter that wraps the loaded
	/// <see cref="AppConfigEx"/> singleton so consumers can receive it via
	/// dependency injection without coupling to <c>Program.Config</c>.
	/// </summary>
	public sealed class AppConfigExOptions : IOptions<AppConfigEx>
	{
		private readonly AppConfigEx m_config;

		public AppConfigExOptions(AppConfigEx config)
		{
			if(config == null) throw new ArgumentNullException("config");
			m_config = config;
		}

		public AppConfigEx Value => m_config;
	}
}
