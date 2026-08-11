using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;

using KeePass.Core.Projections;
using KeePass.Core.Services;

using CoreFilter = KeePass.Core.Services.FileDialogFilter;
using KeePass.Core.ViewModels;

using KeePassLib.Security;

namespace KeePass.Desktop.Avalonia.Views
{
	/// <summary>
	/// Code-behind for <see cref="EntryEditorView"/>.
	///
	/// <para>
	/// Password fields are handled in code-behind to avoid ever exposing the
	/// live password as a plain CLR string property.  All other fields use
	/// MVVM binding through <see cref="EntryEditorViewModel"/>.
	/// </para>
	/// </summary>
	public partial class EntryEditorView : UserControl
	{
		private readonly IFileDialogService? _fileDialogService;

		/// <summary>
		/// Raised when the dialog should be closed after a cancel action.
		/// </summary>
		public event EventHandler? Cancelled;

		public EntryEditorView() : this(null) { }

		public EntryEditorView(IFileDialogService? fileDialogService)
		{
			_fileDialogService = fileDialogService;
			InitializeComponent();
		}

		// ------------------------------------------------------------------ //
		// Password box events                                                  //
		// ------------------------------------------------------------------ //

		private void PasswordBox_TextChanged(object? sender, TextChangedEventArgs e)
		{
			if (DataContext is not EntryEditorViewModel vm) return;
			if (PasswordBox == null) return;

			string text = PasswordBox.Text ?? string.Empty;
			vm.Password = text.Length == 0
				? ProtectedString.Empty
				: new ProtectedString(true, text);
		}

		private void PasswordRepeatBox_TextChanged(object? sender, TextChangedEventArgs e)
		{
			if (DataContext is not EntryEditorViewModel vm) return;
			if (PasswordRepeatBox == null) return;

			string text = PasswordRepeatBox.Text ?? string.Empty;
			vm.PasswordRepeat = text.Length == 0
				? ProtectedString.Empty
				: new ProtectedString(true, text);
		}

		private void PasswordReveal_Checked(object? sender, RoutedEventArgs e)
		{
			if (PasswordBox != null) PasswordBox.PasswordChar = '\0';
		}

		private void PasswordReveal_Unchecked(object? sender, RoutedEventArgs e)
		{
			if (PasswordBox != null) PasswordBox.PasswordChar = '●';
		}

		private void RepeatReveal_Checked(object? sender, RoutedEventArgs e)
		{
			if (PasswordRepeatBox != null) PasswordRepeatBox.PasswordChar = '\0';
		}

		private void RepeatReveal_Unchecked(object? sender, RoutedEventArgs e)
		{
			if (PasswordRepeatBox != null) PasswordRepeatBox.PasswordChar = '●';
		}

		// ------------------------------------------------------------------ //
		// Advanced tab: custom field removal                                   //
		// ------------------------------------------------------------------ //

		private void RemoveFieldButton_Click(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not EntryEditorViewModel vm) return;

			// Find the DataGrid for the Advanced tab and remove the selected row.
			// The DataGrid is named by convention — we iterate descendants.
			var grid = this.FindControl<DataGrid>("CustomFieldGrid");
			if (grid?.SelectedItem is FieldViewModel selected)
				vm.RemoveFieldCommand.Execute(selected);
		}

		// ------------------------------------------------------------------ //
		// Properties tab: tags box                                             //
		// ------------------------------------------------------------------ //

		private void TagsBox_TextChanged(object? sender, TextChangedEventArgs e)
		{
			// Tags are synced on LostFocus to avoid thrashing the collection.
		}

		private void TagsBox_LostFocus(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not EntryEditorViewModel vm) return;
			if (TagsBox == null) return;

			vm.Tags.Clear();
			string rawTags = TagsBox.Text ?? string.Empty;
			foreach (string tag in rawTags.Split(',',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (!string.IsNullOrWhiteSpace(tag))
					vm.Tags.Add(tag);
			}
		}

		// ------------------------------------------------------------------ //
		// Auto-type tab: association removal                                   //
		// ------------------------------------------------------------------ //

		private void RemoveAssocButton_Click(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not EntryEditorViewModel vm) return;

			var grid = this.FindControl<DataGrid>("AssocGrid");
			if (grid?.SelectedItem is AutoTypeAssociationViewModel selected)
				vm.RemoveAssociationCommand.Execute(selected);
		}

		// ------------------------------------------------------------------ //
		// Attachments tab                                                       //
		// ------------------------------------------------------------------ //

		private async void AddAttachButton_Click(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not EntryEditorViewModel vm) return;
			if (_fileDialogService == null) return;

			var filters = new List<CoreFilter>
			{
				new CoreFilter { Name = "All Files", Extensions = new[] { "*" } }
			};

			string? path = await _fileDialogService.OpenFileAsync("Attach File", filters);
			if (string.IsNullOrEmpty(path)) return;

			byte[] content;
			try
			{
				content = await File.ReadAllBytesAsync(path);
			}
			catch (Exception)
			{
				return;
			}

			vm.AddAttachmentCommand.Execute(new AttachmentData
			{
				Name    = Path.GetFileName(path),
				Content = content
			});
		}

		private void RemoveAttachButton_Click(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not EntryEditorViewModel vm) return;
			if (AttachmentList?.SelectedItem is BinaryReference selected)
				vm.RemoveAttachmentCommand.Execute(selected);
		}
	}
}
