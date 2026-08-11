using System;
using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

using KeePass.Core.ViewModels;

using KeePassLib.Security;

namespace KeePass.Desktop.Avalonia.Views
{
	/// <summary>
	/// Code-behind for <see cref="PasswordGeneratorView"/>.
	/// </summary>
	public partial class PasswordGeneratorView : UserControl
	{
		/// <summary>
		/// Raised when the user clicks "Use Password". Provides the generated
		/// password as a <see cref="ProtectedString"/> so callers (e.g. the
		/// entry editor) can consume it without exposing plain text.
		/// </summary>
		public event EventHandler<ProtectedString>? PasswordAccepted;

		/// <summary>Raised when the user clicks Close.</summary>
		public event EventHandler? Closed;

		public PasswordGeneratorView()
		{
			InitializeComponent();
			DataContextChanged += OnDataContextChanged;
		}

		// ------------------------------------------------------------------ //
		// DataContext wiring — keeps the generated-password TextBox in sync   //
		// ------------------------------------------------------------------ //

		private PasswordGeneratorViewModel? _vm;

		private void OnDataContextChanged(object? sender, EventArgs e)
		{
			if (_vm != null) _vm.PropertyChanged -= Vm_PropertyChanged;
			_vm = DataContext as PasswordGeneratorViewModel;
			if (_vm != null) _vm.PropertyChanged += Vm_PropertyChanged;
			SyncGeneratedPasswordBox();
		}

		private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PasswordGeneratorViewModel.GeneratedPassword))
				SyncGeneratedPasswordBox();
		}

		/// <summary>
		/// Reads the plain text from <c>ProtectedString</c> and places it into
		/// the read-only TextBox. This is the only place the password ever
		/// exists as a CLR <see cref="string"/> — the TextBox does not
		/// participate in two-way binding.
		/// </summary>
		private void SyncGeneratedPasswordBox()
		{
			if (GeneratedPasswordBox == null) return;
			if (_vm == null || _vm.GeneratedPassword == null || _vm.GeneratedPassword.IsEmpty)
			{
				GeneratedPasswordBox.Text = string.Empty;
				return;
			}
			GeneratedPasswordBox.Text = _vm.GeneratedPassword.ReadString();
		}

		// ------------------------------------------------------------------ //
		// Reveal toggle                                                        //
		// ------------------------------------------------------------------ //

		private void RevealToggle_Checked(object? sender, RoutedEventArgs e)
		{
			if (GeneratedPasswordBox != null)
				GeneratedPasswordBox.PasswordChar = '\0';
		}

		private void RevealToggle_Unchecked(object? sender, RoutedEventArgs e)
		{
			if (GeneratedPasswordBox != null)
				GeneratedPasswordBox.PasswordChar = '●';
		}

		// ------------------------------------------------------------------ //
		// Copy to clipboard                                                    //
		// ------------------------------------------------------------------ //

		private async void CopyButton_Click(object? sender, RoutedEventArgs e)
		{
			if (DataContext is not PasswordGeneratorViewModel vm) return;
			if (vm.GeneratedPassword == null) return;

			var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
			if (clipboard == null) return;

			string plain = vm.GeneratedPassword.ReadString();
			await clipboard.SetTextAsync(plain);

			// Brief visual feedback: disable button for 1.5 s.
			if (CopyButton != null)
			{
				CopyButton.IsEnabled = false;
				await System.Threading.Tasks.Task.Delay(1500);
				Dispatcher.UIThread.Post(() =>
				{
					if (CopyButton != null) CopyButton.IsEnabled = true;
				});
			}
		}

		// ------------------------------------------------------------------ //
		// Accept / close                                                       //
		// ------------------------------------------------------------------ //

		private void UseButton_Click(object? sender, RoutedEventArgs e)
		{
			if (DataContext is PasswordGeneratorViewModel vm &&
				vm.GeneratedPassword != null)
			{
				PasswordAccepted?.Invoke(this, vm.GeneratedPassword);
			}
			Closed?.Invoke(this, EventArgs.Empty);
		}

		private void CloseButton_Click(object? sender, RoutedEventArgs e)
		{
			Closed?.Invoke(this, EventArgs.Empty);
		}
	}
}
