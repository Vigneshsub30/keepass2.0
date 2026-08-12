using System;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeePass.Desktop.Avalonia.Views
{
	/// <summary>
	/// Code-behind for <see cref="DatabaseSettingsView"/>.
	/// All logic lives in <see cref="KeePass.Core.ViewModels.DatabaseSettingsViewModel"/>;
	/// this file only handles Cancel routing and any events that cannot be
	/// expressed as data bindings.
	/// </summary>
	public partial class DatabaseSettingsView : UserControl
	{
		/// <summary>Raised when the user clicks Cancel.</summary>
		public event EventHandler? Closed;

		public DatabaseSettingsView()
		{
			InitializeComponent();
		}

		private void CancelButton_Click(object? sender, RoutedEventArgs e)
		{
			Closed?.Invoke(this, EventArgs.Empty);
		}
	}
}
