using System.Collections.Generic;

using KeePassLib.Serialization;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Platform-neutral abstraction for discovering suggested key-file paths
	/// for a given database connection. Implementations may search removable
	/// media, recent paths, or other platform-specific locations.
	/// </summary>
	public interface IKeyFileLocator
	{
		/// <summary>
		/// Returns an ordered list of suggested key-file paths for the
		/// supplied database connection. The list may be empty but is never null.
		/// </summary>
		IReadOnlyList<string> GetSuggestedKeyFiles(IOConnectionInfo ioc);
	}
}
