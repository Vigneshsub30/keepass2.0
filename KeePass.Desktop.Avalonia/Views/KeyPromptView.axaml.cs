using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using KeePass.Core.ViewModels;

using KeePassLib.Security;

namespace KeePass.Desktop.Avalonia.Views
{
	/// <summary>
	/// Code-behind for <see cref="KeyPromptView"/>.
	///
	/// <para>
	/// Password handling is done in code-behind rather than via binding to
	/// avoid ever exposing the live plaintext password as a CLR string property
	/// on the ViewModel — the <c>PasswordBox</c> in Avalonia has no
	/// <c>SecureString</c> equivalent, so we move the value into a
	/// <see cref="ProtectedString"/> as quickly as possible and rely on
	/// Avalonia's own <c>TextBox</c> not caching it beyond its internal buffer.
	/// </para>
	/// </summary>
	public partial class KeyPromptView : UserControl
	{
		/// <summary>
		/// Raised when the user explicitly clicks Cancel or presses Escape.
		/// The caller should close or hide the dialog in response.
		/// </summary>
		public event EventHandler? Cancelled;

		public KeyPromptView()
		{
			InitializeComponent();
		}

		// ------------------------------------------------------------------ //
		// Password box events                                                  //
		// ------------------------------------------------------------------ //

		private void PasswordBox_TextChanged(object? sender, TextChangedEventArgs e)
		{
			if (DataContext is not KeyPromptViewModel vm) return;
			if (PasswordBox == null) return;

			string text = PasswordBox.Text ?? string.Empty;
			vm.MasterPassword = text.Length == 0
				? ProtectedString.Empty
				: new ProtectedString(true, text);
		}

		private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Enter)
			{
				if (DataContext is KeyPromptViewModel vm &&
					vm.UnlockCommand.CanExecute(null))
				{
					vm.UnlockCommand.Execute(null);
				}
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				Cancelled?.Invoke(this, EventArgs.Empty);
				e.Handled = true;
			}
		}

		// ------------------------------------------------------------------ //
		// Reveal-password toggle                                               //
		// ------------------------------------------------------------------ //

		private void RevealToggle_Checked(object? sender, RoutedEventArgs e)
		{
			if (PasswordBox != null) PasswordBox.PasswordChar = '\0';
		}

		private void RevealToggle_Unchecked(object? sender, RoutedEventArgs e)
		{
			if (PasswordBox != null) PasswordBox.PasswordChar = '●';
		}

		// ------------------------------------------------------------------ //
		// Cancel button                                                        //
		// ------------------------------------------------------------------ //

		private void CancelButton_Click(object? sender, RoutedEventArgs e)
		{
			Cancelled?.Invoke(this, EventArgs.Empty);
		}
	}
}
