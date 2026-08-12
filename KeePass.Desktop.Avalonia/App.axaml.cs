using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using KeePass.Core.DataExchange;
using KeePass.Core.Platform;
using KeePass.Core.Projections;
using KeePass.Core.Services;
using KeePass.Core.ViewModels;
using KeePass.Desktop.Avalonia.Services;
using KeePass.Desktop.Avalonia.Views;

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;

using Microsoft.Extensions.DependencyInjection;

namespace KeePass.Desktop.Avalonia
{
	public partial class App : Application
	{
		internal IServiceProvider? Services => _services;
		private IServiceProvider? _services;
		private BrowserSocketServer? _browserServer;

		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private MainWindow? _mainWindow;

		public override void OnFrameworkInitializationCompleted()
		{
			_services = BuildServiceProvider(() => _mainWindow);

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var vm = _services.GetRequiredService<MainWindowViewModel>();
				_mainWindow = new MainWindow { DataContext = vm };
				desktop.MainWindow = _mainWindow;

				vm.ExitRequested += (_, _) =>
				{
					_browserServer?.Dispose();
					_browserServer = null;
					desktop.Shutdown();
				};

				// Start the browser integration socket server
				var sessionService = _services.GetRequiredService<IDatabaseSessionService>();
				_browserServer = new BrowserSocketServer(sessionService);
				try { _browserServer.Start(); }
				catch { /* non-fatal if socket cannot be created */ }

				desktop.Exit += (_, _) =>
				{
					_browserServer?.Dispose();
					_browserServer = null;
				};
			}

			base.OnFrameworkInitializationCompleted();
		}

		public static IServiceProvider BuildServiceProvider(Func<Window?>? windowFactory = null)
		{
			var services = new ServiceCollection();

			// Core projection mappers
			services.AddSingleton<EntryProjectionMapper>();
			services.AddSingleton<GroupProjectionMapper>();

			// File dialog service
			if (windowFactory != null)
				services.AddSingleton<IFileDialogService>(
					new AvaloniaFileDialogService(windowFactory));
			else
				services.AddSingleton<IFileDialogService, NullFileDialogService>();

			// Key file locator
			services.AddSingleton<IKeyFileLocator, DefaultKeyFileLocator>();

			// Database session service
			if (windowFactory != null)
			{
				services.AddSingleton<IDatabaseSessionService>(sp =>
					new AvaloniaSessionService(
						sp.GetRequiredService<IFileDialogService>(),
						windowFactory,
						sp.GetRequiredService<IKeyFileLocator>()));
			}
			else
			{
				services.AddSingleton<IDatabaseSessionService, NullDatabaseSessionService>();
			}

			// Password generator profile store
			services.AddSingleton<IGeneratorProfileStore, InMemoryGeneratorProfileStore>();

			// Clipboard service — platform-specific
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				services.AddSingleton<IClipboardService>(new KeePass.Platform.Unix.Mac.MacClipboardService());
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				services.AddSingleton<IClipboardService>(new KeePass.Platform.Unix.Linux.LinuxClipboardService());

			// Format providers for import/export
			var formatProviders = new List<IFileFormatProvider>
			{
				new FileFormatProviderAdapter("KeePassKdbx",   "KeePass KDBX 2.x",            true, true, "kdbx"),
				new FileFormatProviderAdapter("KeePassXml2",   "KeePass XML (2.x)",            true, true, "xml"),
				new FileFormatProviderAdapter("GenericCsv",     "Generic CSV",                  true, true, "csv"),
			};
			services.AddSingleton<IReadOnlyList<IFileFormatProvider>>(formatProviders);

			// Import service with KDBX and CSV importers
			var importers = new Dictionary<string, Func<Stream, PwDatabase, IStatusLogger, bool>>
			{
				["KeePassKdbx"] = (stream, db, logger) =>
				{
					var tempDb = new PwDatabase();
					var ioc = new IOConnectionInfo { Path = "import.kdbx" };
					tempDb.Open(ioc, db.MasterKey, logger);
					return true;
				},
				["GenericCsv"] = (stream, db, logger) =>
				{
					using var reader = new StreamReader(stream);
					string headerLine = reader.ReadLine();
					if (headerLine == null) return false;
					string[] headers = headerLine.Split(',');
					int titleIdx = Array.FindIndex(headers, h => h.Trim().Equals("Title", StringComparison.OrdinalIgnoreCase));
					int userIdx  = Array.FindIndex(headers, h => h.Trim().Equals("UserName", StringComparison.OrdinalIgnoreCase) ||
						h.Trim().Equals("Username", StringComparison.OrdinalIgnoreCase));
					int pwIdx    = Array.FindIndex(headers, h => h.Trim().Equals("Password", StringComparison.OrdinalIgnoreCase));
					int urlIdx   = Array.FindIndex(headers, h => h.Trim().Equals("URL", StringComparison.OrdinalIgnoreCase) ||
						h.Trim().Equals("Url", StringComparison.OrdinalIgnoreCase));
					int noteIdx  = Array.FindIndex(headers, h => h.Trim().Equals("Notes", StringComparison.OrdinalIgnoreCase));

					string line;
					while ((line = reader.ReadLine()) != null)
					{
						if (string.IsNullOrWhiteSpace(line)) continue;
						string[] parts = line.Split(',');
						var pe = new PwEntry(true, true);
						if (titleIdx >= 0 && titleIdx < parts.Length)
							pe.Strings.Set(PwDefs.TitleField, new KeePassLib.Security.ProtectedString(false, parts[titleIdx].Trim()));
						if (userIdx >= 0 && userIdx < parts.Length)
							pe.Strings.Set(PwDefs.UserNameField, new KeePassLib.Security.ProtectedString(false, parts[userIdx].Trim()));
						if (pwIdx >= 0 && pwIdx < parts.Length)
							pe.Strings.Set(PwDefs.PasswordField, new KeePassLib.Security.ProtectedString(true, parts[pwIdx].Trim()));
						if (urlIdx >= 0 && urlIdx < parts.Length)
							pe.Strings.Set(PwDefs.UrlField, new KeePassLib.Security.ProtectedString(false, parts[urlIdx].Trim()));
						if (noteIdx >= 0 && noteIdx < parts.Length)
							pe.Strings.Set(PwDefs.NotesField, new KeePassLib.Security.ProtectedString(false, parts[noteIdx].Trim()));
						db.RootGroup.AddEntry(pe, true);
					}
					return true;
				}
			};
			services.AddSingleton<IImportService>(new ImportService(importers));

			// Export service with CSV exporter
			var exporters = new Dictionary<string, Func<Stream, PwDatabase, IStatusLogger, bool>>
			{
				["GenericCsv"] = (stream, db, logger) =>
				{
					using var writer = new StreamWriter(stream);
					writer.WriteLine("Title,UserName,Password,URL,Notes");
					ExportGroupCsv(db.RootGroup, writer);
					return true;
				},
				["KeePassXml2"] = (stream, db, logger) =>
				{
					var kdbxFile = new KeePassLib.Serialization.KdbxFile(db);
					kdbxFile.Save(stream, null, KdbxFormat.PlainXml, logger);
					return true;
				}
			};
			services.AddSingleton<IExportService>(new ExportService(exporters));

			// View-model layer
			services.AddTransient<MainWindowViewModel>(sp =>
				new MainWindowViewModel(
					sp.GetRequiredService<IDatabaseSessionService>(),
					sp.GetRequiredService<EntryProjectionMapper>(),
					sp.GetRequiredService<GroupProjectionMapper>(),
					clipboardService: sp.GetService<IClipboardService>(),
					profileStore: sp.GetService<IGeneratorProfileStore>()));

			return services.BuildServiceProvider();
		}

		private static void ExportGroupCsv(PwGroup group, StreamWriter writer)
		{
			for (uint i = 0; i < group.Entries.UCount; i++)
			{
				var e = group.Entries.GetAt(i);
				string title = EscapeCsv(e.Strings.ReadSafe(PwDefs.TitleField));
				string user  = EscapeCsv(e.Strings.ReadSafe(PwDefs.UserNameField));
				string pw    = EscapeCsv(e.Strings.GetSafe(PwDefs.PasswordField).ReadString());
				string url   = EscapeCsv(e.Strings.ReadSafe(PwDefs.UrlField));
				string notes = EscapeCsv(e.Strings.ReadSafe(PwDefs.NotesField));
				writer.WriteLine($"{title},{user},{pw},{url},{notes}");
			}
			for (uint i = 0; i < group.Groups.UCount; i++)
				ExportGroupCsv(group.Groups.GetAt(i), writer);
		}

		private static string EscapeCsv(string value)
		{
			if (string.IsNullOrEmpty(value)) return string.Empty;
			if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
				return "\"" + value.Replace("\"", "\"\"") + "\"";
			return value;
		}
	}
}
