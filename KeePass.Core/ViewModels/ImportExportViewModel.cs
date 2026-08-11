#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePass.Core.Services;

using KeePassLib;

using FileFormatProvider = KeePass.Core.Services.IFileFormatProvider;

namespace KeePass.Core.ViewModels
{
	// ======================================================================
	// Format provider wrapper
	// ======================================================================

	/// <summary>
	/// Display wrapper for an <see cref="IFileFormatProvider"/> entry.
	/// </summary>
	public sealed class FormatProviderViewModel : ObservableObject
	{
		public IFileFormatProvider Provider { get; }
		public string Name => Provider.DisplayName;
		public bool SupportsImport => Provider.SupportsImport;
		public bool SupportsExport => Provider.SupportsExport;
		public string DefaultExtension => Provider.DefaultExtension;

		public FormatProviderViewModel(IFileFormatProvider provider)
			=> Provider = provider ?? throw new ArgumentNullException(nameof(provider));
	}

	// ======================================================================
	// Import view-model
	// ======================================================================

	/// <summary>
	/// ViewModel for the Import dialog. Handles format selection, source file
	/// management, merge strategy, and delegates the actual import to
	/// <see cref="IImportService"/>.
	/// </summary>
	public sealed class ImportViewModel : ObservableObject
	{
		private readonly IImportService _importService;
		private readonly IFileDialogService _fileDialog;
		private readonly PwDatabase? _database;

		// ------------------------------------------------------------------ //
		// Format list                                                         //
		// ------------------------------------------------------------------ //

		public ObservableCollection<FormatProviderViewModel> Formats { get; }
			= new ObservableCollection<FormatProviderViewModel>();

		private FormatProviderViewModel? _selectedFormat;
		public FormatProviderViewModel? SelectedFormat
		{
			get => _selectedFormat;
			set
			{
				if (SetProperty(ref _selectedFormat, value))
					OnPropertyChanged(nameof(CanImport));
			}
		}

		// ------------------------------------------------------------------ //
		// Source files                                                        //
		// ------------------------------------------------------------------ //

		public ObservableCollection<string> FilePaths { get; }
			= new ObservableCollection<string>();

		// ------------------------------------------------------------------ //
		// Merge strategy                                                      //
		// ------------------------------------------------------------------ //

		private PwMergeMethod _mergeMethod = PwMergeMethod.CreateNewUuids;
		public PwMergeMethod MergeMethod
		{
			get => _mergeMethod;
			set => SetProperty(ref _mergeMethod, value);
		}

		// ------------------------------------------------------------------ //
		// Progress / status                                                   //
		// ------------------------------------------------------------------ //

		private bool _isImporting;
		public bool IsImporting
		{
			get => _isImporting;
			private set
			{
				if (SetProperty(ref _isImporting, value))
					OnPropertyChanged(nameof(CanImport));
			}
		}

		public ObservableCollection<string> StatusMessages { get; }
			= new ObservableCollection<string>();

		// ------------------------------------------------------------------ //
		// Computed                                                            //
		// ------------------------------------------------------------------ //

		public bool CanImport => _selectedFormat != null && FilePaths.Count > 0 && !_isImporting;

		// ------------------------------------------------------------------ //
		// Commands                                                            //
		// ------------------------------------------------------------------ //

		public IAsyncRelayCommand BrowseFilesCommand { get; }
		public IAsyncRelayCommand ImportCommand { get; }
		public IRelayCommand RemoveFileCommand { get; }
		public IRelayCommand CancelCommand { get; }

		public event EventHandler? Closed;

		// ------------------------------------------------------------------ //
		// Constructor                                                         //
		// ------------------------------------------------------------------ //

		public ImportViewModel(
			IImportService importService,
			IFileDialogService fileDialog,
			PwDatabase? database = null)
		{
			_importService = importService ?? throw new ArgumentNullException(nameof(importService));
			_fileDialog    = fileDialog    ?? throw new ArgumentNullException(nameof(fileDialog));
			_database      = database;

			BrowseFilesCommand = new AsyncRelayCommand(ExecuteBrowseFilesAsync);
			ImportCommand      = new AsyncRelayCommand(ExecuteImportAsync,
				() => CanImport);
			RemoveFileCommand  = new RelayCommand<string>(ExecuteRemoveFile);
			CancelCommand      = new RelayCommand(() => Closed?.Invoke(this, EventArgs.Empty));
		}

		// ------------------------------------------------------------------ //
		// Format loading                                                      //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// Populates <see cref="Formats"/> from an external list of providers.
		/// Only providers that support import are added.
		/// </summary>
		public void LoadFormats(System.Collections.Generic.IEnumerable<IFileFormatProvider> providers)
		{
			Formats.Clear();
			foreach (var p in providers.Where(p => p.SupportsImport))
				Formats.Add(new FormatProviderViewModel(p));
		}

		// ------------------------------------------------------------------ //
		// Browse                                                              //
		// ------------------------------------------------------------------ //

		private async Task ExecuteBrowseFilesAsync()
		{
			string ext = _selectedFormat?.DefaultExtension ?? "*";
			var filters = new System.Collections.Generic.List<FileDialogFilter>
			{
				new FileDialogFilter
				{
					Name = _selectedFormat?.Name ?? "All Files",
					Extensions = new[] { string.IsNullOrEmpty(ext) ? "*" : ext }
				},
				new FileDialogFilter { Name = "All Files", Extensions = new[] { "*" } }
			};

			string? path = await _fileDialog.OpenFileAsync("Import From", filters);
			if (!string.IsNullOrEmpty(path) && !FilePaths.Contains(path))
			{
				FilePaths.Add(path);
				OnPropertyChanged(nameof(CanImport));
			}
		}

		// ------------------------------------------------------------------ //
		// Remove file                                                         //
		// ------------------------------------------------------------------ //

		private void ExecuteRemoveFile(string? path)
		{
			if (path != null) FilePaths.Remove(path);
			OnPropertyChanged(nameof(CanImport));
		}

		// ------------------------------------------------------------------ //
		// Import                                                              //
		// ------------------------------------------------------------------ //

		private async Task ExecuteImportAsync(CancellationToken ct)
		{
			if (_selectedFormat == null) return;

			IsImporting = true;
			StatusMessages.Clear();

			try
			{
				var progress = new Progress<string>(msg =>
					StatusMessages.Add(msg));

				var results = await _importService.ImportAsync(
					_selectedFormat.Provider,
					FilePaths,
					_mergeMethod,
					_database,
					progress,
					ct);

				foreach (var r in results)
				{
					StatusMessages.Add(r.Success
						? $"✓ {System.IO.Path.GetFileName(r.FilePath)}"
						: $"✗ {System.IO.Path.GetFileName(r.FilePath)}: {r.ErrorMessage}");
				}
			}
			catch (OperationCanceledException)
			{
				StatusMessages.Add("Import cancelled.");
			}
			catch (Exception ex)
			{
				StatusMessages.Add($"Import failed: {ex.Message}");
			}
			finally
			{
				IsImporting = false;
			}
		}
	}

	// ======================================================================
	// Export view-model
	// ======================================================================

	/// <summary>
	/// ViewModel for the Export dialog. Handles format selection, destination
	/// path, and delegates to <see cref="IExportService"/>.
	/// </summary>
	public sealed class ExportViewModel : ObservableObject
	{
		private readonly IExportService _exportService;
		private readonly IFileDialogService _fileDialog;
		private readonly PwDatabase? _database;

		// ------------------------------------------------------------------ //
		// Format list                                                         //
		// ------------------------------------------------------------------ //

		public ObservableCollection<FormatProviderViewModel> Formats { get; }
			= new ObservableCollection<FormatProviderViewModel>();

		private FormatProviderViewModel? _selectedFormat;
		public FormatProviderViewModel? SelectedFormat
		{
			get => _selectedFormat;
			set
			{
				if (SetProperty(ref _selectedFormat, value))
					OnPropertyChanged(nameof(CanExport));
			}
		}

		// ------------------------------------------------------------------ //
		// Destination path                                                    //
		// ------------------------------------------------------------------ //

		private string _destinationPath = string.Empty;
		public string DestinationPath
		{
			get => _destinationPath;
			set
			{
				if (SetProperty(ref _destinationPath, value ?? string.Empty))
					OnPropertyChanged(nameof(CanExport));
			}
		}

		// ------------------------------------------------------------------ //
		// Progress                                                            //
		// ------------------------------------------------------------------ //

		private bool _isExporting;
		public bool IsExporting
		{
			get => _isExporting;
			private set
			{
				if (SetProperty(ref _isExporting, value))
					OnPropertyChanged(nameof(CanExport));
			}
		}

		private string _statusMessage = string.Empty;
		public string StatusMessage
		{
			get => _statusMessage;
			private set => SetProperty(ref _statusMessage, value ?? string.Empty);
		}

		// ------------------------------------------------------------------ //
		// Computed                                                            //
		// ------------------------------------------------------------------ //

		public bool CanExport =>
			_selectedFormat != null &&
			!string.IsNullOrWhiteSpace(_destinationPath) &&
			!_isExporting;

		// ------------------------------------------------------------------ //
		// Commands                                                            //
		// ------------------------------------------------------------------ //

		public IAsyncRelayCommand BrowseDestinationCommand { get; }
		public IAsyncRelayCommand ExportCommand { get; }
		public IRelayCommand CancelCommand { get; }

		public event EventHandler? Closed;

		// ------------------------------------------------------------------ //
		// Constructor                                                         //
		// ------------------------------------------------------------------ //

		public ExportViewModel(
			IExportService exportService,
			IFileDialogService fileDialog,
			PwDatabase? database = null)
		{
			_exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
			_fileDialog    = fileDialog    ?? throw new ArgumentNullException(nameof(fileDialog));
			_database      = database;

			BrowseDestinationCommand = new AsyncRelayCommand(ExecuteBrowseDestinationAsync);
			ExportCommand = new AsyncRelayCommand(ExecuteExportAsync,
				() => CanExport);
			CancelCommand = new RelayCommand(() => Closed?.Invoke(this, EventArgs.Empty));
		}

		// ------------------------------------------------------------------ //
		// Format loading                                                      //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// Populates <see cref="Formats"/> from an external list of providers.
		/// Only providers that support export are added.
		/// </summary>
		public void LoadFormats(System.Collections.Generic.IEnumerable<IFileFormatProvider> providers)
		{
			Formats.Clear();
			foreach (var p in providers.Where(p => p.SupportsExport))
				Formats.Add(new FormatProviderViewModel(p));
		}

		// ------------------------------------------------------------------ //
		// Browse                                                              //
		// ------------------------------------------------------------------ //

		private async Task ExecuteBrowseDestinationAsync()
		{
			string ext = _selectedFormat?.DefaultExtension ?? "*";
			var filters = new System.Collections.Generic.List<FileDialogFilter>
			{
				new FileDialogFilter
				{
					Name = _selectedFormat?.Name ?? "All Files",
					Extensions = new[] { string.IsNullOrEmpty(ext) ? "*" : ext }
				},
				new FileDialogFilter { Name = "All Files", Extensions = new[] { "*" } }
			};

			string? path = await _fileDialog.OpenFileAsync("Export To", filters);
			if (!string.IsNullOrEmpty(path))
				DestinationPath = path;
		}

		// ------------------------------------------------------------------ //
		// Export                                                              //
		// ------------------------------------------------------------------ //

		private async Task ExecuteExportAsync(CancellationToken ct)
		{
			if (_selectedFormat == null || string.IsNullOrWhiteSpace(_destinationPath))
				return;

			IsExporting = true;
			StatusMessage = "Exporting…";

			try
			{
				var progress = new Progress<string>(msg => StatusMessage = msg);

				bool ok = await _exportService.ExportAsync(
					_selectedFormat.Provider,
					_destinationPath,
					_database,
					progress,
					ct);

				StatusMessage = ok ? "Export completed." : "Export failed.";
			}
			catch (OperationCanceledException)
			{
				StatusMessage = "Export cancelled.";
			}
			catch (Exception ex)
			{
				StatusMessage = $"Export failed: {ex.Message}";
			}
			finally
			{
				IsExporting = false;
			}
		}
	}
}
