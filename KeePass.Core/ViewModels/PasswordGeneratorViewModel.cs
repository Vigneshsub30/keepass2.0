using System;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePass.Core.Services;

using KeePassLib.Cryptography;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for the password generator dialog. Exposes all
	/// <see cref="PwProfile"/> settings as observable properties, generates
	/// passwords synchronously via <see cref="PwGenerator.Generate"/>, and
	/// manages named generator profiles. No WinForms references.
	/// </summary>
	public sealed class PasswordGeneratorViewModel : ObservableObject
	{
		private readonly IGeneratorProfileStore _profileStore;

		// ------------------------------------------------------------------ //
		// Generator type and length                                           //
		// ------------------------------------------------------------------ //

		private PasswordGeneratorType _generatorType = PasswordGeneratorType.CharSet;
		public PasswordGeneratorType GeneratorType
		{
			get => _generatorType;
			set
			{
				if (SetProperty(ref _generatorType, value))
					OnPropertyChanged(nameof(HasSecurityReducingOption));
			}
		}

		private uint _length = 20;
		public uint Length
		{
			get => _length;
			set => SetProperty(ref _length, value);
		}

		// ------------------------------------------------------------------ //
		// CharSet range flags                                                  //
		// ------------------------------------------------------------------ //

		private bool _useUpperCase = true;
		public bool UseUpperCase
		{
			get => _useUpperCase;
			set => SetProperty(ref _useUpperCase, value);
		}

		private bool _useLowerCase = true;
		public bool UseLowerCase
		{
			get => _useLowerCase;
			set => SetProperty(ref _useLowerCase, value);
		}

		private bool _useDigits = true;
		public bool UseDigits
		{
			get => _useDigits;
			set => SetProperty(ref _useDigits, value);
		}

		private bool _useSpecial;
		public bool UseSpecial
		{
			get => _useSpecial;
			set => SetProperty(ref _useSpecial, value);
		}

		private bool _useBrackets;
		public bool UseBrackets
		{
			get => _useBrackets;
			set => SetProperty(ref _useBrackets, value);
		}

		private bool _useLatin1;
		public bool UseLatin1
		{
			get => _useLatin1;
			set => SetProperty(ref _useLatin1, value);
		}

		/// <summary>Additional custom characters beyond the named ranges.</summary>
		private string _charSetAdditional = string.Empty;
		public string CharSetAdditional
		{
			get => _charSetAdditional;
			set => SetProperty(ref _charSetAdditional, value ?? string.Empty);
		}

		// ------------------------------------------------------------------ //
		// Pattern-mode settings                                                //
		// ------------------------------------------------------------------ //

		private string _pattern = string.Empty;
		public string Pattern
		{
			get => _pattern;
			set => SetProperty(ref _pattern, value ?? string.Empty);
		}

		private bool _patternPermutePassword;
		public bool PatternPermutePassword
		{
			get => _patternPermutePassword;
			set => SetProperty(ref _patternPermutePassword, value);
		}

		// ------------------------------------------------------------------ //
		// Security options                                                     //
		// ------------------------------------------------------------------ //

		private bool _excludeLookAlike;
		public bool ExcludeLookAlike
		{
			get => _excludeLookAlike;
			set
			{
				if (SetProperty(ref _excludeLookAlike, value))
					OnPropertyChanged(nameof(HasSecurityReducingOption));
			}
		}

		private bool _noRepeatingCharacters;
		public bool NoRepeatingCharacters
		{
			get => _noRepeatingCharacters;
			set
			{
				if (SetProperty(ref _noRepeatingCharacters, value))
					OnPropertyChanged(nameof(HasSecurityReducingOption));
			}
		}

		private string _excludeCharacters = string.Empty;
		public string ExcludeCharacters
		{
			get => _excludeCharacters;
			set
			{
				if (SetProperty(ref _excludeCharacters, value ?? string.Empty))
					OnPropertyChanged(nameof(HasSecurityReducingOption));
			}
		}

		// ------------------------------------------------------------------ //
		// Custom algorithm                                                     //
		// ------------------------------------------------------------------ //

		private string _customAlgorithmUuid = string.Empty;
		public string CustomAlgorithmUuid
		{
			get => _customAlgorithmUuid;
			set => SetProperty(ref _customAlgorithmUuid, value ?? string.Empty);
		}

		// ------------------------------------------------------------------ //
		// Computed / derived properties                                        //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// True when any security-reducing option is enabled
		/// (<see cref="ExcludeLookAlike"/>, <see cref="NoRepeatingCharacters"/>,
		/// or non-empty <see cref="ExcludeCharacters"/>).
		/// </summary>
		public bool HasSecurityReducingOption =>
			_excludeLookAlike || _noRepeatingCharacters || (_excludeCharacters.Length != 0);

		// ------------------------------------------------------------------ //
		// Generation results                                                   //
		// ------------------------------------------------------------------ //

		private ProtectedString _generatedPassword = ProtectedString.Empty;
		public ProtectedString GeneratedPassword
		{
			get => _generatedPassword;
			private set
			{
				if (SetProperty(ref _generatedPassword, value))
					OnPropertyChanged(nameof(HasGeneratedPassword));
			}
		}

		/// <summary>True when a non-empty password has been generated.</summary>
		public bool HasGeneratedPassword => !_generatedPassword.IsEmpty;

		private uint _passwordQualityBits;
		public uint PasswordQualityBits
		{
			get => _passwordQualityBits;
			private set => SetProperty(ref _passwordQualityBits, value);
		}

		private string _generationError = string.Empty;
		public string GenerationError
		{
			get => _generationError;
			private set => SetProperty(ref _generationError, value ?? string.Empty);
		}

		// ------------------------------------------------------------------ //
		// Profile management                                                   //
		// ------------------------------------------------------------------ //

		public ObservableCollection<PwProfile> Profiles { get; } =
			new ObservableCollection<PwProfile>();

		private PwProfile? _selectedProfile;
		public PwProfile? SelectedProfile
		{
			get => _selectedProfile;
			set => SetProperty(ref _selectedProfile, value);
		}

		// ------------------------------------------------------------------ //
		// Commands                                                             //
		// ------------------------------------------------------------------ //

		public IRelayCommand GenerateCommand { get; }
		public IRelayCommand SaveProfileCommand { get; }
		public IRelayCommand LoadProfileCommand { get; }
		public IRelayCommand DeleteProfileCommand { get; }

		// ------------------------------------------------------------------ //
		// Constructor                                                          //
		// ------------------------------------------------------------------ //

		public PasswordGeneratorViewModel(IGeneratorProfileStore profileStore)
		{
			_profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));

			GenerateCommand = new RelayCommand(ExecuteGenerate);
			SaveProfileCommand = new RelayCommand<string>(ExecuteSaveProfile);
			LoadProfileCommand = new RelayCommand<PwProfile>(ExecuteLoadProfile);
			DeleteProfileCommand = new RelayCommand<PwProfile>(ExecuteDeleteProfile);

			LoadProfilesFromStore();
		}

		// ------------------------------------------------------------------ //
		// Generation                                                           //
		// ------------------------------------------------------------------ //

		private void ExecuteGenerate()
		{
			PwProfile profile = BuildProfile();
			GenerationError = string.Empty;

			PwgError err = PwGenerator.Generate(out ProtectedString ps, profile,
				null, null); // no user entropy, no custom algorithm pool

			if (err == PwgError.Success && ps != null && !ps.IsEmpty)
			{
				GeneratedPassword = ps;
				PasswordQualityBits = ComputeQualityBits(ps);
			}
			else
			{
				GeneratedPassword = ProtectedString.Empty;
				PasswordQualityBits = 0;
				GenerationError = err == PwgError.Success
					? "Generated password was empty."
					: $"Generation failed: {err}.";
			}
		}

		private static uint ComputeQualityBits(ProtectedString ps)
		{
			byte[] pbUtf8 = ps.ReadUtf8();
			try
			{
				return QualityEstimation.EstimatePasswordBits(pbUtf8);
			}
			catch
			{
				// Degrade gracefully if PopularPasswords is not initialised.
				return 0u;
			}
			finally
			{
				MemUtil.ZeroByteArray(pbUtf8);
			}
		}

		// ------------------------------------------------------------------ //
		// Profile management                                                   //
		// ------------------------------------------------------------------ //

		private void ExecuteSaveProfile(string? name)
		{
			if (string.IsNullOrWhiteSpace(name)) return;

			PwProfile profile = BuildProfile();
			profile.Name = name!;

			_profileStore.SaveProfile(profile);

			for (int i = Profiles.Count - 1; i >= 0; i--)
			{
				if (string.Equals(Profiles[i].Name, name, StringComparison.OrdinalIgnoreCase))
					Profiles.RemoveAt(i);
			}

			Profiles.Add(profile);
		}

		private void ExecuteLoadProfile(PwProfile? profile)
		{
			if (profile == null) return;
			ApplyProfile(profile);
			SelectedProfile = profile;
		}

		private void ExecuteDeleteProfile(PwProfile? profile)
		{
			if (profile == null) return;

			_profileStore.DeleteProfile(profile.Name);
			Profiles.Remove(profile);

			if (ReferenceEquals(_selectedProfile, profile))
				SelectedProfile = null;
		}

		private void LoadProfilesFromStore()
		{
			foreach (var p in _profileStore.GetProfiles())
				Profiles.Add(p);
		}

		// ------------------------------------------------------------------ //
		// PwProfile ↔ ViewModel conversion                                   //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// Constructs a <see cref="PwProfile"/> that reflects the current
		/// ViewModel property values.
		/// </summary>
		/// <remarks>
		/// The CharSet is built directly as a <see cref="PwCharSet"/> and
		/// assigned via the backing property to avoid triggering the internal
		/// <c>UpdateCharSet</c> logic which requires a fully-formed packed
		/// <c>CharSetRanges</c> string.
		/// </remarks>
		public PwProfile BuildProfile()
		{
			// Build a single flat CharSet that includes all named ranges and
			// any additional custom characters. This avoids going through
			// PwProfile.CharSetAdditional/CharSetRanges which require a
			// pre-packed 10-character ranges string.
			var cs = new PwCharSet();
			if (_useUpperCase) cs.Add(PwCharSet.UpperCase);
			if (_useLowerCase) cs.Add(PwCharSet.LowerCase);
			if (_useDigits) cs.Add(PwCharSet.Digits);
			if (_useSpecial) cs.Add(PwCharSet.Special);
			if (_useBrackets) cs.Add(PwCharSet.Brackets);
			if (_useLatin1) cs.Add(PwCharSet.Latin1S);
			if (!string.IsNullOrEmpty(_charSetAdditional)) cs.Add(_charSetAdditional);

			var profile = new PwProfile();
			profile.GeneratorType = _generatorType;
			profile.Length = _length;
			profile.CharSet = cs; // direct assignment — no UpdateCharSet side effect
			profile.Pattern = _pattern;
			profile.PatternPermutePassword = _patternPermutePassword;
			profile.ExcludeLookAlike = _excludeLookAlike;
			profile.NoRepeatingCharacters = _noRepeatingCharacters;
			profile.ExcludeCharacters = _excludeCharacters;
			profile.CustomAlgorithmUuid = _customAlgorithmUuid;
			return profile;
		}

		/// <summary>
		/// Populates ViewModel properties from an existing <see cref="PwProfile"/>.
		/// </summary>
		/// <remarks>
		/// CharSet flags are derived by checking whether each named range is
		/// fully contained in <see cref="PwProfile.CharSet"/>. Reading
		/// <see cref="PwProfile.CharSetAdditional"/> (which triggers
		/// <c>UpdateCharSet</c>) is deliberately avoided when the profile was
		/// built via <see cref="BuildProfile"/>, as those internal strings may
		/// not be in the packed format that <c>UnpackCharRanges</c> requires.
		/// </remarks>
		public void ApplyProfile(PwProfile profile)
		{
			if (profile == null) throw new ArgumentNullException(nameof(profile));

			GeneratorType = profile.GeneratorType;
			Length = profile.Length;

			// Derive boolean flags from the resolved CharSet (the getter is safe
			// only if CharSet is the primary source of truth).
			PwCharSet cs = profile.CharSet;
			UseUpperCase = cs.Contains(PwCharSet.UpperCase);
			UseLowerCase = cs.Contains(PwCharSet.LowerCase);
			UseDigits = cs.Contains(PwCharSet.Digits);
			UseSpecial = cs.Contains(PwCharSet.Special);
			UseBrackets = cs.Contains(PwCharSet.Brackets);
			UseLatin1 = cs.Contains(PwCharSet.Latin1S);

			// Don't read CharSetAdditional – it triggers UpdateCharSet which
			// requires a packed CharSetRanges. Leave CharSetAdditional at default.
			CharSetAdditional = string.Empty;
			Pattern = profile.Pattern;
			PatternPermutePassword = profile.PatternPermutePassword;
			ExcludeLookAlike = profile.ExcludeLookAlike;
			NoRepeatingCharacters = profile.NoRepeatingCharacters;
			ExcludeCharacters = profile.ExcludeCharacters;
			CustomAlgorithmUuid = profile.CustomAlgorithmUuid;
		}
	}
}
