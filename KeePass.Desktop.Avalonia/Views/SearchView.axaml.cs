using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using KeePass.Core.ViewModels;

namespace KeePass.Desktop.Avalonia.Views
{
	/// <summary>
	/// Code-behind for <see cref="SearchView"/>.
	/// </summary>
	public partial class SearchView : UserControl
	{
		/// <summary>Raised when the user closes the search panel.</summary>
		public event EventHandler? Closed;

		public SearchView()
		{
			InitializeComponent();
		}

		private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Enter)
			{
				if (DataContext is SearchViewModel vm &&
					vm.SearchCommand.CanExecute(null))
				{
					vm.SearchCommand.Execute(null);
				}
				e.Handled = true;
			}
		}

		private void CloseButton_Click(object? sender, RoutedEventArgs e)
		{
			Closed?.Invoke(this, EventArgs.Empty);
		}
	}
}
