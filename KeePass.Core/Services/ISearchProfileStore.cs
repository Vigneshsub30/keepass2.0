using System.Collections.Generic;

using KeePassLib;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Platform-neutral persistence abstraction for named search profiles.
	/// Implementations may store profiles in XML configuration, a database,
	/// or any other medium.
	/// </summary>
	public interface ISearchProfileStore
	{
		/// <summary>
		/// Returns all persisted search profiles. The list may be empty but is never null.
		/// </summary>
		IReadOnlyList<SearchParameters> GetProfiles();

		/// <summary>
		/// Persists <paramref name="profile"/>. If a profile with the same
		/// <see cref="SearchParameters.Name"/> already exists it is replaced.
		/// </summary>
		void SaveProfile(SearchParameters profile);

		/// <summary>
		/// Removes the profile with the given <paramref name="name"/>.
		/// Does nothing if no such profile exists.
		/// </summary>
		void DeleteProfile(string name);
	}
}
