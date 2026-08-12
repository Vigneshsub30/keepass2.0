#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using KeePassLib;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Unit tests for <see cref="ImportViewModel"/> and <see cref="ExportViewModel"/>.
	/// </summary>
	public sealed class ImportExportViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Stubs                                                               //
		// ------------------------------------------------------------------ //

		private sealed class FakeFormatProvider : IFileFormatProvider
		{
			public string FormatName    { get; }
			public string DisplayName   { get; }
			public bool   SupportsImport { get; }
			public bool   SupportsExport { get; }
			public string DefaultExtension { get; }

			public FakeFormatProvider(string name, bool import = true, bool export = true, string ext = "csv")
			{
				FormatName     = name;
				DisplayName    = name;
				SupportsImport = import;
				SupportsExport = export;
				DefaultExtension = ext;
			}
		}

		private sealed class SucceedingImportService : IImportService
		{
			public int CallCount { get; private set; }

			public Task<IReadOnlyList<ImportFileResult>> ImportAsync(
				IFileFormatProvider format, IEnumerable<string> filePaths,
				PwMergeMethod mergeMethod, PwDatabase database,
				IProgress<string>? progress, CancellationToken ct)
			{
				CallCount++;
				var results = filePaths
					.Select(fp => new ImportFileResult(fp, true))
					.ToList();
				return Task.FromResult<IReadOnlyList<ImportFileResult>>(results);
			}
		}

		private sealed class FailingImportService : IImportService
		{
			public Task<IReadOnlyList<ImportFileResult>> ImportAsync(
				IFileFormatProvider format, IEnumerable<string> filePaths,
				PwMergeMethod mergeMethod, PwDatabase database,
				IProgress<string>? progress, CancellationToken ct)
			{
				var results = filePaths
					.Select(fp => new ImportFileResult(fp, false, "Format not supported"))
					.ToList();
				return Task.FromResult<IReadOnlyList<ImportFileResult>>(results);
			}
		}

		private sealed class SucceedingExportService : IExportService
		{
			public int CallCount { get; private set; }

			public Task<bool> ExportAsync(
				IFileFormatProvider format, string destinationPath,
				PwDatabase database, IProgress<string>? progress, CancellationToken ct)
			{
				CallCount++;
				return Task.FromResult(true);
			}
		}

		private sealed class FailingExportService : IExportService
		{
			public Task<bool> ExportAsync(
				IFileFormatProvider format, string destinationPath,
				PwDatabase database, IProgress<string>? progress, CancellationToken ct)
				=> Task.FromResult(false);
		}

		private sealed class ReturningFileDialogService : IFileDialogService
		{
			public string? ReturnPath { get; set; }

			public Task<string?> OpenFileAsync(string title, IReadOnlyList<FileDialogFilter> filters)
				=> Task.FromResult(ReturnPath);

			public Task<string?> SaveFileAsync(string title, IReadOnlyList<FileDialogFilter> filters, string? defaultFileName = null)
				=> Task.FromResult(ReturnPath);
		}

		// ------------------------------------------------------------------ //
		// Helpers                                                             //
		// ------------------------------------------------------------------ //

		private static ImportViewModel CreateImportVm(
			IImportService? svc = null,
			IFileDialogService? dlg = null,
			IEnumerable<IFileFormatProvider>? formats = null)
		{
			var vm = new ImportViewModel(
				svc ?? new SucceedingImportService(),
				dlg ?? new ReturningFileDialogService { ReturnPath = null },
				null);
			vm.LoadFormats(formats ?? DefaultFormats());
			return vm;
		}

		private static ExportViewModel CreateExportVm(
			IExportService? svc = null,
			IFileDialogService? dlg = null,
			IEnumerable<IFileFormatProvider>? formats = null)
		{
			var vm = new ExportViewModel(
				svc ?? new SucceedingExportService(),
				dlg ?? new ReturningFileDialogService { ReturnPath = null },
				null);
			vm.LoadFormats(formats ?? DefaultFormats());
			return vm;
		}

		private static IEnumerable<IFileFormatProvider> DefaultFormats() => new[]
		{
			new FakeFormatProvider("KeePass CSV", import: true, export: true,  ext: "csv"),
			new FakeFormatProvider("1Password",   import: true, export: false, ext: "1pif"),
			new FakeFormatProvider("HTML Export", import: false, export: true, ext: "html"),
		};

		// ------------------------------------------------------------------ //
		// ImportViewModel — format loading                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ImportVm_LoadFormats_OnlyIncludesImportableFormats()
		{
			var vm = CreateImportVm();
			Assert.All(vm.Formats, f => Assert.True(f.SupportsImport));
			Assert.DoesNotContain(vm.Formats, f => f.Name == "HTML Export");
		}

		[Fact]
		public void ImportVm_LoadFormats_PopulatesFormatList()
		{
			var vm = CreateImportVm();
			Assert.Equal(2, vm.Formats.Count); // CSV + 1Password
		}

		// ------------------------------------------------------------------ //
		// ImportViewModel — CanImport computed flag                          //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ImportVm_CanImport_FalseWhenNoFormatSelected()
		{
			var vm = CreateImportVm();
			vm.FilePaths.Add("/tmp/a.csv");
			Assert.False(vm.CanImport); // no format selected
		}

		[Fact]
		public void ImportVm_CanImport_FalseWhenNoFiles()
		{
			var vm = CreateImportVm();
			vm.SelectedFormat = vm.Formats[0];
			Assert.False(vm.CanImport);
		}

		[Fact]
		public void ImportVm_CanImport_TrueWhenFormatAndFileSelected()
		{
			var vm = CreateImportVm();
			vm.SelectedFormat = vm.Formats[0];
			vm.FilePaths.Add("/tmp/import.csv");
			Assert.True(vm.CanImport);
		}

		// ------------------------------------------------------------------ //
		// ImportViewModel — RemoveFileCommand                                 //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ImportVm_RemoveFileCommand_RemovesPathFromCollection()
		{
			var vm = CreateImportVm();
			vm.FilePaths.Add("/tmp/a.csv");
			vm.FilePaths.Add("/tmp/b.csv");

			vm.RemoveFileCommand.Execute("/tmp/a.csv");

			Assert.DoesNotContain("/tmp/a.csv", vm.FilePaths);
			Assert.Contains("/tmp/b.csv", vm.FilePaths);
		}

		// ------------------------------------------------------------------ //
		// ImportViewModel — BrowseFilesCommand                                //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task ImportVm_BrowseFiles_AddsReturnedPath()
		{
			var dlg = new ReturningFileDialogService { ReturnPath = "/home/user/export.csv" };
			var vm = CreateImportVm(dlg: dlg);
			vm.SelectedFormat = vm.Formats[0];

			await vm.BrowseFilesCommand.ExecuteAsync(null);

			Assert.Contains("/home/user/export.csv", vm.FilePaths);
		}

		[Fact]
		public async Task ImportVm_BrowseFiles_NullPathIsIgnored()
		{
			var dlg = new ReturningFileDialogService { ReturnPath = null };
			var vm = CreateImportVm(dlg: dlg);

			await vm.BrowseFilesCommand.ExecuteAsync(null);

			Assert.Empty(vm.FilePaths);
		}

		// ------------------------------------------------------------------ //
		// ImportViewModel — ImportCommand                                     //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task ImportVm_ImportCommand_CallsImportService()
		{
			var svc = new SucceedingImportService();
			var vm  = CreateImportVm(svc: svc);
			vm.SelectedFormat = vm.Formats[0];
			vm.FilePaths.Add("/tmp/import.csv");

			await vm.ImportCommand.ExecuteAsync(null);

			Assert.Equal(1, svc.CallCount);
		}

		[Fact]
		public async Task ImportVm_ImportCommand_AddsSuccessStatusMessage()
		{
			var vm = CreateImportVm();
			vm.SelectedFormat = vm.Formats[0];
			vm.FilePaths.Add("/tmp/import.csv");

			await vm.ImportCommand.ExecuteAsync(null);

			Assert.Contains(vm.StatusMessages, m => m.StartsWith("✓"));
		}

		[Fact]
		public async Task ImportVm_ImportCommand_AddsFailureStatusMessage()
		{
			var vm = CreateImportVm(svc: new FailingImportService());
			vm.SelectedFormat = vm.Formats[0];
			vm.FilePaths.Add("/tmp/import.csv");

			await vm.ImportCommand.ExecuteAsync(null);

			Assert.Contains(vm.StatusMessages, m => m.StartsWith("✗"));
		}

		// ------------------------------------------------------------------ //
		// ImportViewModel — MergeMethod                                       //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ImportVm_DefaultMergeMethod_IsCreateNewUuids()
		{
			var vm = CreateImportVm();
			Assert.Equal(PwMergeMethod.CreateNewUuids, vm.MergeMethod);
		}

		[Fact]
		public void ImportVm_MergeMethod_CanBeChangedToSynchronize()
		{
			var vm = CreateImportVm();
			vm.MergeMethod = PwMergeMethod.Synchronize;
			Assert.Equal(PwMergeMethod.Synchronize, vm.MergeMethod);
		}

		// ------------------------------------------------------------------ //
		// ExportViewModel — format loading                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ExportVm_LoadFormats_OnlyIncludesExportableFormats()
		{
			var vm = CreateExportVm();
			Assert.All(vm.Formats, f => Assert.True(f.SupportsExport));
			Assert.DoesNotContain(vm.Formats, f => f.Name == "1Password");
		}

		[Fact]
		public void ExportVm_LoadFormats_PopulatesFormatList()
		{
			var vm = CreateExportVm();
			Assert.Equal(2, vm.Formats.Count); // CSV + HTML
		}

		// ------------------------------------------------------------------ //
		// ExportViewModel — CanExport                                         //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ExportVm_CanExport_FalseWhenNoFormatSelected()
		{
			var vm = CreateExportVm();
			vm.DestinationPath = "/tmp/out.csv";
			Assert.False(vm.CanExport);
		}

		[Fact]
		public void ExportVm_CanExport_FalseWhenNoDestination()
		{
			var vm = CreateExportVm();
			vm.SelectedFormat = vm.Formats[0];
			Assert.False(vm.CanExport);
		}

		[Fact]
		public void ExportVm_CanExport_TrueWhenFormatAndDestinationSet()
		{
			var vm = CreateExportVm();
			vm.SelectedFormat = vm.Formats[0];
			vm.DestinationPath = "/tmp/out.csv";
			Assert.True(vm.CanExport);
		}

		// ------------------------------------------------------------------ //
		// ExportViewModel — BrowseDestination                                //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task ExportVm_BrowseDestination_SetsDestinationPath()
		{
			var dlg = new ReturningFileDialogService { ReturnPath = "/home/user/passwords.csv" };
			var vm  = CreateExportVm(dlg: dlg);

			await vm.BrowseDestinationCommand.ExecuteAsync(null);

			Assert.Equal("/home/user/passwords.csv", vm.DestinationPath);
		}

		// ------------------------------------------------------------------ //
		// ExportViewModel — ExportCommand                                     //
		// ------------------------------------------------------------------ //

		[Fact]
		public async Task ExportVm_ExportCommand_CallsExportService()
		{
			var svc = new SucceedingExportService();
			var vm  = CreateExportVm(svc: svc);
			vm.SelectedFormat  = vm.Formats[0];
			vm.DestinationPath = "/tmp/out.csv";

			await vm.ExportCommand.ExecuteAsync(null);

			Assert.Equal(1, svc.CallCount);
		}

		[Fact]
		public async Task ExportVm_ExportCommand_SetsCompletedStatus()
		{
			var vm = CreateExportVm();
			vm.SelectedFormat  = vm.Formats[0];
			vm.DestinationPath = "/tmp/out.csv";

			await vm.ExportCommand.ExecuteAsync(null);

			Assert.Equal("Export completed.", vm.StatusMessage);
		}

		[Fact]
		public async Task ExportVm_ExportCommand_SetsFailedStatus()
		{
			var vm = CreateExportVm(svc: new FailingExportService());
			vm.SelectedFormat  = vm.Formats[0];
			vm.DestinationPath = "/tmp/out.csv";

			await vm.ExportCommand.ExecuteAsync(null);

			Assert.Equal("Export failed.", vm.StatusMessage);
		}

		// ------------------------------------------------------------------ //
		// CancelCommand                                                       //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ImportVm_CancelCommand_RaisesClosedEvent()
		{
			var vm = CreateImportVm();
			bool closed = false;
			vm.Closed += (_, _) => closed = true;

			vm.CancelCommand.Execute(null);

			Assert.True(closed);
		}

		[Fact]
		public void ExportVm_CancelCommand_RaisesClosedEvent()
		{
			var vm = CreateExportVm();
			bool closed = false;
			vm.Closed += (_, _) => closed = true;

			vm.CancelCommand.Execute(null);

			Assert.True(closed);
		}
	}
}
