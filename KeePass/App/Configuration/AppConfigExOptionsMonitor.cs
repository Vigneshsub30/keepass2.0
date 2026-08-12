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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Extensions.Options;

namespace KeePass.App.Configuration
{
	/// <summary>
	/// <see cref="IOptionsMonitor{T}"/> implementation for <see cref="AppConfigEx"/>.
	/// Supports runtime configuration reload by watching the user-config XML file
	/// for changes on disk and notifying registered <see cref="OnChange"/> listeners.
	/// </summary>
	public sealed class AppConfigExOptionsMonitor : IOptionsMonitor<AppConfigEx>,
		IDisposable
	{
		private volatile AppConfigEx m_current;
		private readonly List<Action<AppConfigEx, string>> m_listeners =
			new List<Action<AppConfigEx, string>>();
		private FileSystemWatcher m_watcher;

		public AppConfigExOptionsMonitor(AppConfigEx initial)
		{
			if(initial == null) throw new ArgumentNullException("initial");
			m_current = initial;
		}

		/// <summary>
		/// Starts a <see cref="FileSystemWatcher"/> on the user-configuration file
		/// so that <see cref="OnChange"/> subscribers are notified when it is modified.
		/// Calling this multiple times safely replaces the previous watcher.
		/// Silently no-ops when <paramref name="configFilePath"/> is empty or the
		/// directory does not exist.
		/// </summary>
		public void WatchConfigFile(string configFilePath)
		{
			if(string.IsNullOrEmpty(configFilePath)) return;

			string dir = Path.GetDirectoryName(configFilePath);
			string file = Path.GetFileName(configFilePath);
			if(string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file)) return;
			if(!Directory.Exists(dir)) return;

			m_watcher?.Dispose();

			m_watcher = new FileSystemWatcher(dir, file)
			{
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
				EnableRaisingEvents = true
			};

			// Changed fires multiple times during an atomic write; the duplicate
			// reloads are harmless because AppConfigSerializer.Load is idempotent.
			m_watcher.Changed += OnFileChanged;
		}

		private void OnFileChanged(object sender, FileSystemEventArgs e)
		{
			AppConfigEx loaded = null;
			try { loaded = AppConfigSerializer.Load(); }
			catch(Exception ex) { Debug.Assert(false, ex.Message); }

			if(loaded == null) return;

			m_current = loaded;

			// Inform Program so that Program.Config reflects the new instance.
			Program.ReplaceConfig(loaded);

			Action<AppConfigEx, string>[] snapshot;
			lock(m_listeners)
			{
				snapshot = m_listeners.ToArray();
			}

			foreach(Action<AppConfigEx, string> listener in snapshot)
			{
				try { listener(m_current, Options.DefaultName); }
				catch(Exception ex) { Debug.Assert(false, ex.Message); }
			}
		}

		public AppConfigEx CurrentValue => m_current;

		public AppConfigEx Get(string name) => m_current;

		public IDisposable OnChange(Action<AppConfigEx, string> listener)
		{
			if(listener == null) throw new ArgumentNullException("listener");

			lock(m_listeners) { m_listeners.Add(listener); }
			return new ChangeRegistration(this, listener);
		}

		public void Dispose()
		{
			m_watcher?.Dispose();
			m_watcher = null;
		}

		private sealed class ChangeRegistration : IDisposable
		{
			private readonly AppConfigExOptionsMonitor m_monitor;
			private readonly Action<AppConfigEx, string> m_listener;
			private bool m_disposed;

			public ChangeRegistration(AppConfigExOptionsMonitor monitor,
				Action<AppConfigEx, string> listener)
			{
				m_monitor = monitor;
				m_listener = listener;
			}

			public void Dispose()
			{
				if(m_disposed) return;
				m_disposed = true;
				lock(m_monitor.m_listeners) { m_monitor.m_listeners.Remove(m_listener); }
			}
		}
	}
}
