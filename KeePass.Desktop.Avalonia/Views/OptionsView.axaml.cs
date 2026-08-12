using System;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeePass.Desktop.Avalonia.Views
{
	public partial class OptionsView : UserControl
	{
		public event EventHandler? Closed;

		public OptionsView()
		{
			InitializeComponent();
		}

		private void CancelButton_Click(object? sender, RoutedEventArgs e)
		{
			Closed?.Invoke(this, EventArgs.Empty);
		}
	}
}
