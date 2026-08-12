using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using KeePass.Core.Services;

using KeePassLib.Serialization;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// Discovers key files by scanning the database file's parent directory
	/// for <c>.keyx</c> and <c>.key</c> files.
	/// </summary>
	internal sealed class DefaultKeyFileLocator : IKeyFileLocator
	{
		public IReadOnlyList<string> GetSuggestedKeyFiles(IOConnectionInfo ioc)
		{
			if (ioc == null || string.IsNullOrEmpty(ioc.Path))
				return Array.Empty<string>();

			string? dir = Path.GetDirectoryName(ioc.Path);
			if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
				return Array.Empty<string>();

			try
			{
				return Directory.GetFiles(dir, "*.keyx")
					.Concat(Directory.GetFiles(dir, "*.key"))
					.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
					.ToArray();
			}
			catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
			catch (IOException) { return Array.Empty<string>(); }
		}
	}
}
