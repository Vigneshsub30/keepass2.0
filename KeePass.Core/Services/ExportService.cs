using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using KeePassLib;
using KeePassLib.Interfaces;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Performs export operations by writing database content to a file
	/// in the requested format. Decoupled from WinForms dialogs.
	/// </summary>
	public sealed class ExportService : IExportService
	{
		private readonly IReadOnlyDictionary<string, Func<Stream, PwDatabase, IStatusLogger, bool>> _exporters;

		public ExportService(
			IReadOnlyDictionary<string, Func<Stream, PwDatabase, IStatusLogger, bool>> exporters)
		{
			_exporters = exporters ?? throw new ArgumentNullException(nameof(exporters));
		}

		public Task<bool> ExportAsync(
			IFileFormatProvider format,
			string destinationPath,
			PwDatabase database,
			IProgress<string> progress,
			CancellationToken ct)
		{
			if (format == null) throw new ArgumentNullException(nameof(format));
			if (database == null) throw new ArgumentNullException(nameof(database));
			if (string.IsNullOrEmpty(destinationPath))
				throw new ArgumentException("Destination path is required.", nameof(destinationPath));

			return Task.Run(() =>
			{
				ct.ThrowIfCancellationRequested();
				progress?.Report($"Exporting to {Path.GetFileName(destinationPath)}…");

				if (!_exporters.TryGetValue(format.FormatName, out var exporter))
					throw new NotSupportedException(
						$"No exporter registered for format '{format.FormatName}'.");

				using var stream = File.Create(destinationPath);
				return exporter(stream, database, null);
			}, ct);
		}
	}
}
