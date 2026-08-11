using Avalonia.Controls;
using Avalonia.Interactivity;

using KeePass.Core.ViewModels;

using KeePassLib;

namespace KeePass.Desktop.Avalonia.Views
{
	public partial class ImportView : UserControl
	{
		public ImportView()
		{
			InitializeComponent();
			// Select "Create New UUIDs" by default.
			if (MergeCombo != null) MergeCombo.SelectedIndex = 0;
		}

		private void RemoveFileButton_Click(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not ImportViewModel vm) return;
			if (FileList?.SelectedItem is string selected)
				vm.RemoveFileCommand.Execute(selected);
		}

		private void MergeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (DataContext is not ImportViewModel vm) return;
			if (MergeCombo?.SelectedIndex is int idx)
			{
				vm.MergeMethod = idx switch
				{
					0 => PwMergeMethod.CreateNewUuids,
					1 => PwMergeMethod.OverwriteExisting,
					2 => PwMergeMethod.KeepExisting,
					3 => PwMergeMethod.Synchronize,
					_ => PwMergeMethod.CreateNewUuids
				};
			}
		}
	}
}
