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
	/// Performs import operations by reading source files and merging
	/// their content into a <see cref="PwDatabase"/>. Decoupled from
	/// WinForms dialogs — uses streams and progress callbacks.
	/// </summary>
	public sealed class ImportService : IImportService
	{
		private readonly IReadOnlyDictionary<string, Func<Stream, PwDatabase, IStatusLogger, bool>> _importers;

		public ImportService(
			IReadOnlyDictionary<string, Func<Stream, PwDatabase, IStatusLogger, bool>> importers)
		{
			_importers = importers ?? throw new ArgumentNullException(nameof(importers));
		}

		public Task<IReadOnlyList<ImportFileResult>> ImportAsync(
			IFileFormatProvider format,
			IEnumerable<string> filePaths,
			PwMergeMethod mergeMethod,
			PwDatabase database,
			IProgress<string> progress,
			CancellationToken ct)
		{
			if (format == null) throw new ArgumentNullException(nameof(format));
			if (database == null) throw new ArgumentNullException(nameof(database));

			return Task.Run(() =>
			{
				var results = new List<ImportFileResult>();

				if (!_importers.TryGetValue(format.FormatName, out var importer))
				{
					foreach (string path in filePaths)
						results.Add(new ImportFileResult(path, false,
							$"No importer registered for format '{format.FormatName}'."));
					return (IReadOnlyList<ImportFileResult>)results;
				}

				foreach (string path in filePaths)
				{
					ct.ThrowIfCancellationRequested();
					progress?.Report($"Importing {Path.GetFileName(path)}…");

					try
					{
						using var stream = File.OpenRead(path);
						var tempDb = new PwDatabase();
						tempDb.New(new KeePassLib.Serialization.IOConnectionInfo(), new KeePassLib.Keys.CompositeKey());

						importer(stream, tempDb, null);

						database.MergeIn(tempDb, mergeMethod);
						results.Add(new ImportFileResult(path, true));
					}
					catch (Exception ex)
					{
						results.Add(new ImportFileResult(path, false, ex.Message));
					}
				}

				database.Modified = true;
				return (IReadOnlyList<ImportFileResult>)results;
			}, ct);
		}
	}
}
