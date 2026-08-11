using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using KeePassLib;

using CoreFormatProvider = KeePass.Core.Services.IFileFormatProvider;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Result of an import operation for a single source file.
	/// </summary>
	public sealed class ImportFileResult
	{
		public string FilePath { get; }
		public bool Success { get; }
		public string? ErrorMessage { get; }

		public ImportFileResult(string filePath, bool success, string? errorMessage = null)
		{
			FilePath     = filePath;
			Success      = success;
			ErrorMessage = errorMessage;
		}
	}

	/// <summary>
	/// Service that performs the actual import operation on a background thread.
	/// Implemented by the desktop project using <c>ImportUtil</c>.
	/// </summary>
	public interface IImportService
	{
		/// <summary>
		/// Imports one or more files into <paramref name="database"/> using the
		/// given <paramref name="format"/> and <paramref name="mergeMethod"/>.
		/// </summary>
		Task<IReadOnlyList<ImportFileResult>> ImportAsync(
			CoreFormatProvider format,
			IEnumerable<string> filePaths,
			PwMergeMethod mergeMethod,
			PwDatabase database,
			IProgress<string>? progress,
			CancellationToken ct);
	}

	/// <summary>
	/// Service that performs the actual export operation on a background thread.
	/// Implemented by the desktop project using <c>ExportUtil</c>.
	/// </summary>
	public interface IExportService
	{
		/// <summary>
		/// Exports <paramref name="database"/> to <paramref name="destinationPath"/>
		/// in the given <paramref name="format"/>.
		/// </summary>
		Task<bool> ExportAsync(
			CoreFormatProvider format,
			string destinationPath,
			PwDatabase database,
			IProgress<string>? progress,
			CancellationToken ct);
	}
}
