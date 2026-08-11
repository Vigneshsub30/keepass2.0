namespace KeePass.Services
{
	/// <summary>
	/// Provides path-comparison logic for MRU list deduplication without
	/// requiring the caller to take a dependency on UI or controller layers.
	/// </summary>
	public interface IMruComparisonService
	{
		/// <summary>
		/// Returns <c>true</c> if <paramref name="path1"/> and
		/// <paramref name="path2"/> refer to the same path, using a
		/// case-insensitive, platform-aware comparison.
		/// </summary>
		bool AreSamePath(string? path1, string? path2);
	}
}
