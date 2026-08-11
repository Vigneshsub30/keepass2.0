using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using KeePass.Core.Projections;
using KeePass.Core.Services;
using KeePass.Core.ViewModels;
using KeePass.Desktop.Avalonia.Services;
using KeePass.Desktop.Avalonia.Views;

using Microsoft.Extensions.DependencyInjection;

namespace KeePass.Desktop.Avalonia
{
	public partial class App : Application
	{
		private IServiceProvider? _services;

		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private MainWindow? _mainWindow;

		public override void OnFrameworkInitializationCompleted()
		{
			_services = BuildServiceProvider();

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var vm = _services.GetRequiredService<MainWindowViewModel>();
				_mainWindow = new MainWindow { DataContext = vm };
				desktop.MainWindow = _mainWindow;
			}

			base.OnFrameworkInitializationCompleted();
		}

		/// <summary>
		/// Builds and configures the application DI container.
		/// Platform-specific service registrations should be added here when
		/// Avalonia platform projects are created.
		/// </summary>
		public static IServiceProvider BuildServiceProvider()
		{
			var services = new ServiceCollection();

			// Core projection mappers
			services.AddSingleton<EntryProjectionMapper>();
			services.AddSingleton<GroupProjectionMapper>();

			// Null session service as placeholder until a real implementation
			// is registered by the desktop platform assembly.
			services.AddSingleton<IDatabaseSessionService, NullDatabaseSessionService>();

			// File dialog service — returns null in headless contexts.
			services.AddSingleton<IFileDialogService, NullFileDialogService>();

			// View-model layer
			services.AddTransient<MainWindowViewModel>(sp =>
				new MainWindowViewModel(
					sp.GetRequiredService<IDatabaseSessionService>(),
					sp.GetRequiredService<EntryProjectionMapper>(),
					sp.GetRequiredService<GroupProjectionMapper>()));

			return services.BuildServiceProvider();
		}
	}
}
