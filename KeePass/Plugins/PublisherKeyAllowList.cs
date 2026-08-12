using System;
using System.Collections.Generic;

using KeePass.App.Configuration;

namespace KeePass.Plugins
{
	/// <summary>
	/// Manages the list of publisher public key tokens that are trusted to
	/// supply plugin assemblies.
	/// </summary>
	public sealed class PublisherKeyAllowList
	{
		private readonly HashSet<string> _tokens;

		/// <summary>
		/// <see langword="true"/> when no publisher tokens have been
		/// configured, meaning the allow-list is not enforced and any
		/// publisher (or unsigned plugin) is accepted at the signing-check
		/// level.  Other checks (MetadataLoadContext inspection) still apply.
		/// </summary>
		public bool IsEmpty => _tokens.Count == 0;

		/// <param name="tokens">
		/// Hex-encoded public key tokens (case-insensitive).  Passing
		/// an empty collection results in an empty (non-enforced) list.
		/// </param>
		public PublisherKeyAllowList(IEnumerable<string> tokens)
		{
			if (tokens == null) throw new ArgumentNullException(nameof(tokens));
			_tokens = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Loads the allow-list from <see cref="AceSecurity.TrustedPluginPublishers"/>.
		/// </summary>
		public static PublisherKeyAllowList FromConfiguration(AceSecurity security)
		{
			if (security == null) throw new ArgumentNullException(nameof(security));
			return new PublisherKeyAllowList(security.TrustedPluginPublishers);
		}

		/// <summary>
		/// Returns <see langword="true"/> when the list is empty (not enforced)
		/// or when <paramref name="hexKeyToken"/> appears in the allow-list.
		/// </summary>
		public bool IsAllowed(string? hexKeyToken)
		{
			if (_tokens.Count == 0) return true; // list not enforced
			if (string.IsNullOrEmpty(hexKeyToken)) return false;
			return _tokens.Contains(hexKeyToken);
		}
	}
}
