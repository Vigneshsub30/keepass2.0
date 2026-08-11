using System;

using Avalonia.Controls;
using Avalonia.Interactivity;

using KeePass.Core.ViewModels;

namespace KeePass.Desktop.Avalonia.Views
{
	/// <summary>
	/// Code-behind for <see cref="GroupEditorView"/>.
	/// </summary>
	public partial class GroupEditorView : UserControl
	{
		/// <summary>
		/// Raised when the user explicitly cancels, requesting the host to close.
		/// </summary>
		public event EventHandler? Cancelled;

		public GroupEditorView()
		{
			InitializeComponent();
		}

		// ------------------------------------------------------------------ //
		// Icon picker                                                          //
		// ------------------------------------------------------------------ //

		private void ChangeIconButton_Click(object? sender, RoutedEventArgs e)
		{
			// The real icon picker would open a popup/dialog.
			// In this skeleton, we have no platform dialog service here —
			// icon picking is wired up by the host dialog window.
		}

		// ------------------------------------------------------------------ //
		// Tags                                                                 //
		// ------------------------------------------------------------------ //

		private void TagsBox_LostFocus(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not GroupEditorViewModel vm) return;
			if (TagsBox == null) return;

			vm.Tags.Clear();
			string raw = TagsBox.Text ?? string.Empty;
			foreach (string tag in raw.Split(',',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (!string.IsNullOrWhiteSpace(tag))
					vm.Tags.Add(tag);
			}
		}
	}
}
