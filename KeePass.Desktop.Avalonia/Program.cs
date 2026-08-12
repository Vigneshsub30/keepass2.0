using Avalonia;

namespace KeePass.Desktop.Avalonia
{
	internal sealed class Program
	{
		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called; things
		// aren't initialized yet and stuff might break.
		[System.STAThread]
		public static void Main(string[] args) =>
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

		/// <summary>
		/// Avalonia configuration — keep platform-specific services out of here.
		/// </summary>
		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.LogToTrace();
	}
}
