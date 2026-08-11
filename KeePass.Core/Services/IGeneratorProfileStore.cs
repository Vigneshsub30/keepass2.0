using System.Collections.Generic;

using KeePassLib.Cryptography.PasswordGenerator;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Platform-neutral persistence abstraction for named password generator profiles.
	/// Implementations may store profiles in application XML configuration, a database,
	/// or any other medium.
	/// </summary>
	public interface IGeneratorProfileStore
	{
		/// <summary>
		/// Returns all persisted generator profiles. The list may be empty but is never null.
		/// </summary>
		IReadOnlyList<PwProfile> GetProfiles();

		/// <summary>
		/// Persists <paramref name="profile"/>. If a profile with the same
		/// <see cref="PwProfile.Name"/> already exists it is replaced.
		/// </summary>
		void SaveProfile(PwProfile profile);

		/// <summary>
		/// Removes the profile with the given <paramref name="name"/>.
		/// Does nothing if no such profile exists.
		/// </summary>
		void DeleteProfile(string name);
	}
}
