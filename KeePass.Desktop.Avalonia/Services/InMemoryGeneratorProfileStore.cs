using System;
using System.Collections.Generic;
using System.Linq;

using KeePass.Core.Services;

using KeePassLib.Cryptography.PasswordGenerator;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// In-memory password generator profile store. Profiles persist for the
	/// lifetime of the application process but are not saved to disk.
	/// </summary>
	internal sealed class InMemoryGeneratorProfileStore : IGeneratorProfileStore
	{
		private readonly List<PwProfile> _profiles = new();

		public InMemoryGeneratorProfileStore()
		{
			_profiles.Add(CreateDefault("20-char alphanumeric", 20,
				upper: true, lower: true, digits: true));
			_profiles.Add(CreateDefault("40-char full", 40,
				upper: true, lower: true, digits: true, special: true));
		}

		public IReadOnlyList<PwProfile> GetProfiles() => _profiles.ToList();

		public void SaveProfile(PwProfile profile)
		{
			if (profile == null) throw new ArgumentNullException(nameof(profile));
			int idx = _profiles.FindIndex(p =>
				string.Equals(p.Name, profile.Name, StringComparison.Ordinal));
			if (idx >= 0)
				_profiles[idx] = profile;
			else
				_profiles.Add(profile);
		}

		public void DeleteProfile(string name)
		{
			_profiles.RemoveAll(p =>
				string.Equals(p.Name, name, StringComparison.Ordinal));
		}

		private static PwProfile CreateDefault(string name, uint length,
			bool upper = false, bool lower = false,
			bool digits = false, bool special = false)
		{
			var p = new PwProfile();
			p.Name = name;
			p.GeneratorType = PasswordGeneratorType.CharSet;
			p.Length = length;
			p.CharSet = new PwCharSet();
			if (upper) p.CharSet.Add(PwCharSet.UpperCase);
			if (lower) p.CharSet.Add(PwCharSet.LowerCase);
			if (digits) p.CharSet.Add(PwCharSet.Digits);
			if (special) p.CharSet.Add(PwCharSet.PrintableAsciiSpecial);
			return p;
		}
	}
}
