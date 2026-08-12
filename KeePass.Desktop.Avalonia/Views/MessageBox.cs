using System.Threading.Tasks;

using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace KeePass.Desktop.Avalonia.Views
{
	internal enum MessageBoxButtons { Ok, YesNoCancel }
	internal enum MessageBoxResult { Ok, Yes, No, Cancel }

	internal static class MessageBox
	{
		public static async Task<MessageBoxResult> Show(
			Window owner, string message, string title,
			MessageBoxButtons buttons = MessageBoxButtons.Ok)
		{
			var result = MessageBoxResult.Cancel;

			var textBlock = new TextBlock
			{
				Text = message,
				TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
				Margin = new global::Avalonia.Thickness(16)
			};

			var buttonPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Spacing = 8,
				Margin = new global::Avalonia.Thickness(16, 0, 16, 16)
			};

			Window dialog = null;

			if (buttons == MessageBoxButtons.Ok)
			{
				var okBtn = new Button { Content = "OK", MinWidth = 80 };
				okBtn.Click += (_, _) => { result = MessageBoxResult.Ok; dialog?.Close(); };
				buttonPanel.Children.Add(okBtn);
			}
			else if (buttons == MessageBoxButtons.YesNoCancel)
			{
				var yesBtn = new Button { Content = "Yes", MinWidth = 80 };
				yesBtn.Click += (_, _) => { result = MessageBoxResult.Yes; dialog?.Close(); };
				var noBtn = new Button { Content = "No", MinWidth = 80 };
				noBtn.Click += (_, _) => { result = MessageBoxResult.No; dialog?.Close(); };
				var cancelBtn = new Button { Content = "Cancel", MinWidth = 80 };
				cancelBtn.Click += (_, _) => { result = MessageBoxResult.Cancel; dialog?.Close(); };
				buttonPanel.Children.Add(yesBtn);
				buttonPanel.Children.Add(noBtn);
				buttonPanel.Children.Add(cancelBtn);
			}

			var stack = new StackPanel();
			stack.Children.Add(textBlock);
			stack.Children.Add(buttonPanel);

			dialog = new Window
			{
				Title = title,
				Content = stack,
				Width = 450,
				SizeToContent = SizeToContent.Height,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				CanResize = false
			};

			await dialog.ShowDialog(owner);
			return result;
		}
	}
}
