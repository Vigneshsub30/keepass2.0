using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Threading;

using KeePass.Core.Services;
using KeePass.Core.ViewModels;
using KeePass.Desktop.Avalonia.Services;

using KeePassLib;
using KeePassLib.Security;

namespace KeePass.Desktop.Avalonia.Views
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			DataContextChanged += OnDataContextChanged;
		}

		private MainWindowViewModel _vm;

		private void OnDataContextChanged(object sender, EventArgs e)
		{
			if (_vm != null)
			{
				_vm.EntryEditorRequested -= OnEntryEditorRequested;
				_vm.PasswordGeneratorRequested -= OnPasswordGeneratorRequested;
				_vm.ImportRequested -= OnImportRequested;
				_vm.ExportRequested -= OnExportRequested;
			}

			_vm = DataContext as MainWindowViewModel;
			if (_vm == null) return;

			_vm.EntryEditorRequested += OnEntryEditorRequested;
			_vm.PasswordGeneratorRequested += OnPasswordGeneratorRequested;
			_vm.ImportRequested += OnImportRequested;
			_vm.ExportRequested += OnExportRequested;

			var grid = this.FindControl<DataGrid>("EntryDataGrid");
			if (grid != null)
				grid.SelectionChanged += EntryDataGrid_SelectionChanged;

			var browserMenuItem = this.FindControl<MenuItem>("BrowserIntegrationMenuItem");
			if (browserMenuItem != null)
				browserMenuItem.Click += OnBrowserIntegrationClicked;
		}

		private void EntryDataGrid_DoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
		{
			if (_vm?.EditEntryCommand?.CanExecute(null) == true)
				_vm.EditEntryCommand.Execute(null);
		}

		private void EntryDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_vm == null) return;
			var grid = sender as DataGrid;
			if (grid == null) return;

			var projections = grid.SelectedItems
				.OfType<KeePass.Core.Projections.EntryProjection>()
				.ToList();

			_vm.SelectedEntries =
				new System.Collections.ObjectModel.ObservableCollection<
					KeePass.Core.Projections.EntryProjection>(projections);
		}

		private async void OnEntryEditorRequested(object sender, EntryEditorRequestEventArgs args)
		{
			var editorView = new EntryEditorView();
			editorView.DataContext = args.ViewModel;

			var dialog = new Window
			{
				Title = args.ViewModel.IsCreateMode ? "New Entry" : "Edit Entry",
				Content = editorView,
				Width = 650,
				Height = 600,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				CanResize = true
			};

			args.ViewModel.Saved += (s, resultEntry) =>
			{
				if (args.ViewModel.IsCreateMode && resultEntry != null && args.TargetGroup != null)
				{
					args.TargetGroup.AddEntry(resultEntry, true);
					args.Database.Modified = true;
				}
				else if (!args.ViewModel.IsCreateMode)
				{
					args.Database.Modified = true;
				}
				Dispatcher.UIThread.Post(() => dialog.Close());
			};

			args.ViewModel.Cancelled += (s, _) =>
			{
				Dispatcher.UIThread.Post(() => dialog.Close());
			};

			await dialog.ShowDialog(this);
			_vm?.SaveDatabaseCommand?.Execute(null);
			if (_vm != null)
			{
				// Force refresh after dialog closes
				typeof(MainWindowViewModel)
					.GetMethod("RefreshAll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
					?.Invoke(_vm, null);
			}
		}

		private async void OnPasswordGeneratorRequested(object sender, EventArgs e)
		{
			IServiceProvider sp = (global::Avalonia.Application.Current as App)?.Services;
			IGeneratorProfileStore store = sp != null
				? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
					.GetService<IGeneratorProfileStore>(sp)
				: null;

			var vm = new PasswordGeneratorViewModel(
				store ?? new Services.InMemoryGeneratorProfileStore());

			var genView = new PasswordGeneratorView();
			genView.DataContext = vm;

			var dialog = new Window
			{
				Title = "Password Generator",
				Content = genView,
				Width = 560,
				Height = 620,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				CanResize = true
			};

			genView.Closed += (s, _) =>
			{
				Dispatcher.UIThread.Post(() => dialog.Close());
			};

			await dialog.ShowDialog(this);
		}

		private async void OnImportRequested(object sender, EventArgs e)
		{
			IServiceProvider sp = (global::Avalonia.Application.Current as App)?.Services;
			if (sp == null || _vm == null) return;

			var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetRequiredService<IDatabaseSessionService>(sp).GetActiveDatabase();
			if (db == null || !db.IsOpen) return;

			var importService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetService<IImportService>(sp);
			var fileDialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetService<IFileDialogService>(sp);
			var formats = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetService<System.Collections.Generic.IReadOnlyList<IFileFormatProvider>>(sp);

			if (importService == null || fileDialog == null) return;

			var vm = new ImportViewModel(importService, fileDialog, db);
			if (formats != null) vm.LoadFormats(formats);

			var importView = new ImportView { DataContext = vm };
			var dialog = new Window
			{
				Title = "Import",
				Content = importView,
				Width = 540,
				Height = 540,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				CanResize = true
			};

			vm.Closed += (s, _) =>
			{
				Dispatcher.UIThread.Post(() => dialog.Close());
			};

			await dialog.ShowDialog(this);

			typeof(MainWindowViewModel)
				.GetMethod("RefreshAll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				?.Invoke(_vm, null);
		}

		private async void OnBrowserIntegrationClicked(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			try
			{
				string proxyPath = NativeMessagingInstaller.GetDefaultProxyPath();
				List<string> installed = NativeMessagingInstaller.GetInstalledBrowsers();

				if (installed.Count > 0)
				{
					var result = await MessageBox.Show(this,
						$"Browser integration is already configured for {installed.Count} browser(s).\n\n" +
						"Do you want to reinstall or remove the integration?",
						"Browser Integration",
						MessageBoxButtons.YesNoCancel);

					if (result == MessageBoxResult.Yes)
					{
						var written = NativeMessagingInstaller.Install(proxyPath);
						await MessageBox.Show(this,
							$"Installed manifests for {written.Count} browser(s).\n\n" +
							string.Join("\n", written),
							"Browser Integration");
					}
					else if (result == MessageBoxResult.No)
					{
						var removed = NativeMessagingInstaller.Uninstall();
						await MessageBox.Show(this,
							$"Removed {removed.Count} manifest(s).",
							"Browser Integration");
					}
				}
				else
				{
					if (!System.IO.File.Exists(proxyPath))
					{
						await MessageBox.Show(this,
							$"Proxy binary not found at:\n{proxyPath}\n\n" +
							"Please ensure the application was installed correctly.",
							"Browser Integration");
						return;
					}

					var written = NativeMessagingInstaller.Install(proxyPath);
					await MessageBox.Show(this,
						$"Browser integration enabled for {written.Count} browser(s).\n\n" +
						string.Join("\n", written) +
						"\n\nInstall the KeePassXC-Browser extension in your browser to connect.",
						"Browser Integration");
				}
			}
			catch (Exception ex)
			{
				await MessageBox.Show(this,
					$"Error: {ex.Message}",
					"Browser Integration");
			}
		}

		private async void OnExportRequested(object sender, EventArgs e)
		{
			IServiceProvider sp = (global::Avalonia.Application.Current as App)?.Services;
			if (sp == null || _vm == null) return;

			var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetRequiredService<IDatabaseSessionService>(sp).GetActiveDatabase();
			if (db == null || !db.IsOpen) return;

			var exportService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetService<IExportService>(sp);
			var fileDialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetService<IFileDialogService>(sp);
			var formats = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
				.GetService<System.Collections.Generic.IReadOnlyList<IFileFormatProvider>>(sp);

			if (exportService == null || fileDialog == null) return;

			var vm = new ExportViewModel(exportService, fileDialog, db);
			if (formats != null) vm.LoadFormats(formats);

			var exportView = new ExportView { DataContext = vm };
			var dialog = new Window
			{
				Title = "Export",
				Content = exportView,
				Width = 520,
				Height = 460,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				CanResize = true
			};

			vm.Closed += (s, _) =>
			{
				Dispatcher.UIThread.Post(() => dialog.Close());
			};

			await dialog.ShowDialog(this);
		}
	}
}
