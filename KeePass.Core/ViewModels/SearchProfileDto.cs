using KeePassLib;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Lightweight wrapper around <see cref="SearchParameters"/> that carries
	/// the profile name for display in the UI without exposing the mutable
	/// domain object directly.
	/// </summary>
	public sealed class SearchProfileDto
	{
		public string Name { get; }

		/// <summary>A snapshot of the parameters at the time the profile was saved.</summary>
		public SearchParameters Parameters { get; }

		internal SearchProfileDto(SearchParameters parameters)
		{
			Parameters = parameters;
			Name = parameters.Name;
		}

		public override string ToString() => Name;
	}
}
