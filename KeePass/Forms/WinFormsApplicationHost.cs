using System;
using System.Threading.Tasks;

using KeePassLib.Plugins;

namespace KeePass.Forms
{
	/// <summary>
	/// WinForms implementation of <see cref="IApplicationHost"/> that delegates
	/// to a <see cref="WinFormsMainWindowService"/> and, where needed, to
	/// <c>MainForm</c> directly for UI-thread marshalling.
	/// </summary>
	public sealed class WinFormsApplicationHost : IApplicationHost
	{
		private readonly MainForm _mainForm;
		private readonly WinFormsMainWindowService _mainWindowService;

		public WinFormsApplicationHost(MainForm mainForm)
		{
			_mainForm          = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
			_mainWindowService = new WinFormsMainWindowService(mainForm);
		}

		// ── IApplicationHost ─────────────────────────────────────────── //

		public bool IsMainWindowVisible => _mainWindowService.IsVisible;

		public void BringToForeground() => _mainWindowService.BringToForeground();

		public void ShowStatusMessage(string message)
		{
			// MainForm exposes SetStatusEx for plugin status updates.
			if (_mainForm.InvokeRequired)
				_mainForm.Invoke(new Action(() => _mainForm.SetStatusEx(message)));
			else
				_mainForm.SetStatusEx(message);
		}

		public void InvokeOnUIThread(Action action)
		{
			if (_mainForm.InvokeRequired)
				_mainForm.Invoke(action);
			else
				action();
		}

		public Task InvokeOnUIThreadAsync(Action action)
		{
			var tcs = new TaskCompletionSource<bool>();
			if (_mainForm.InvokeRequired)
			{
				_mainForm.BeginInvoke(new Action(() =>
				{
					try   { action(); tcs.SetResult(true); }
					catch (Exception ex) { tcs.SetException(ex); }
				}));
			}
			else
			{
				try   { action(); tcs.SetResult(true); }
				catch (Exception ex) { tcs.SetException(ex); }
			}
			return tcs.Task;
		}

		public void RefreshEntryList()   => _mainWindowService.RefreshEntryList();
		public bool SaveAllDatabases()   => _mainWindowService.SaveAllDatabases();

		public string PlatformName => "WinForms";

		/// <summary>
		/// Access to the raw <see cref="IMainWindowService"/> for callers that
		/// need the legacy service contract (e.g. <see cref="LegacyPluginHostAdapter"/>).
		/// </summary>
		public IMainWindowService MainWindowService => _mainWindowService;
	}
}
