using System;
using System.Threading.Tasks;

namespace KeePassLib.Plugins
{
	/// <summary>
	/// Platform-neutral application-level host interface that plugins use to
	/// interact with the running KeePass instance without depending on any
	/// specific UI framework type (no <c>System.Windows.Forms</c> or
	/// <c>Avalonia</c> references).
	/// </summary>
	public interface IApplicationHost
	{
		// ── Window state ─────────────────────────────────────────────── //

		/// <summary>Whether the main application window is currently visible.</summary>
		bool IsMainWindowVisible { get; }

		/// <summary>Brings the main window to the foreground.</summary>
		void BringToForeground();

		// ── Status ───────────────────────────────────────────────────── //

		/// <summary>
		/// Shows a short transient status message in the application's status
		/// bar or equivalent UI element.
		/// </summary>
		void ShowStatusMessage(string message);

		// ── UI threading ─────────────────────────────────────────────── //

		/// <summary>
		/// Invokes <paramref name="action"/> on the UI thread if necessary,
		/// or runs it inline if the caller is already on the UI thread.
		/// </summary>
		void InvokeOnUIThread(Action action);

		/// <summary>
		/// Asynchronously posts <paramref name="action"/> to the UI thread.
		/// Returns a task that completes when the action finishes.
		/// </summary>
		Task InvokeOnUIThreadAsync(Action action);

		// ── Data ─────────────────────────────────────────────────────── //

		/// <summary>
		/// Refreshes the visible entry list in the main window.
		/// No-op when the main window is not currently visible.
		/// </summary>
		void RefreshEntryList();

		/// <summary>
		/// Saves all open databases.
		/// </summary>
		/// <returns><see langword="true"/> if all saves succeeded.</returns>
		bool SaveAllDatabases();

		/// <summary>
		/// Optional platform capability descriptor, e.g. "WinForms", "Avalonia",
		/// "Headless". Plugins may inspect this for optional platform features.
		/// </summary>
		string PlatformName { get; }
	}
}
