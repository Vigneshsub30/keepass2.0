using KeePassLib.Utility;

namespace KeePass.Services
{
	/// <summary>
	/// Default implementation of <see cref="IMruComparisonService"/>.
	/// Uses <c>OrdinalIgnoreCase</c> (via <see cref="StrUtil.CaseIgnoreCmp"/>)
	/// matching the comparison already used throughout MruList.
	/// </summary>
	public sealed class MruComparisonService : IMruComparisonService
	{
		/// <summary>
		/// Singleton for use-sites that do not have a DI container.
		/// </summary>
		public static readonly MruComparisonService Instance = new MruComparisonService();

		/// <inheritdoc/>
		public bool AreSamePath(string? path1, string? path2)
		{
			if(path1 == null && path2 == null) return true;
			if(path1 == null || path2 == null) return false;
			return path1.Equals(path2, StrUtil.CaseIgnoreCmp);
		}
	}
}
