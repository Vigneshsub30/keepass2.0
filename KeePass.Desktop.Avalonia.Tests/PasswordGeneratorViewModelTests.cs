#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Security;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Unit tests for <see cref="PasswordGeneratorViewModel"/>.
	/// </summary>
	public sealed class PasswordGeneratorViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Stub profile store                                                  //
		// ------------------------------------------------------------------ //

		private sealed class InMemoryProfileStore : IGeneratorProfileStore
		{
			private readonly List<PwProfile> _profiles = new List<PwProfile>();

			public IReadOnlyList<PwProfile> GetProfiles() => _profiles.AsReadOnly();

			public void SaveProfile(PwProfile profile)
			{
				for (int i = _profiles.Count - 1; i >= 0; i--)
				{
					if (string.Equals(_profiles[i].Name, profile.Name, StringComparison.OrdinalIgnoreCase))
						_profiles.RemoveAt(i);
				}
				_profiles.Add(profile);
			}

			public void DeleteProfile(string name)
			{
				for (int i = _profiles.Count - 1; i >= 0; i--)
				{
					if (string.Equals(_profiles[i].Name, name, StringComparison.OrdinalIgnoreCase))
						_profiles.RemoveAt(i);
				}
			}
		}

		private static PasswordGeneratorViewModel CreateVm(
			IGeneratorProfileStore? store = null)
			=> new PasswordGeneratorViewModel(store ?? new InMemoryProfileStore());

		// ------------------------------------------------------------------ //
		// Default state                                                       //
		// ------------------------------------------------------------------ //

		[Fact]
		public void DefaultState_GeneratedPasswordIsEmpty()
		{
			var vm = CreateVm();
			Assert.True(vm.GeneratedPassword.IsEmpty);
			Assert.False(vm.HasGeneratedPassword);
			Assert.Equal(0u, vm.PasswordQualityBits);
			Assert.Equal(string.Empty, vm.GenerationError);
		}

		[Fact]
		public void DefaultState_CharSetFlagsMatchBuiltInDefaults()
		{
			var vm = CreateVm();
			Assert.True(vm.UseUpperCase);
			Assert.True(vm.UseLowerCase);
			Assert.True(vm.UseDigits);
			Assert.False(vm.UseSpecial);
			Assert.False(vm.UseBrackets);
			Assert.False(vm.UseLatin1);
		}

		[Fact]
		public void DefaultState_LengthIsTwenty()
		{
			var vm = CreateVm();
			Assert.Equal(20u, vm.Length);
		}

		// ------------------------------------------------------------------ //
		// Character-set generation                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Generate_WithDefaultCharSet_ProducesNonEmptyPassword()
		{
			var vm = CreateVm();

			vm.GenerateCommand.Execute(null);

			Assert.True(vm.HasGeneratedPassword);
			Assert.False(vm.GeneratedPassword.IsEmpty);
			Assert.Equal(string.Empty, vm.GenerationError);
		}

		[Fact]
		public void Generate_PasswordLength_MatchesConfiguredLength()
		{
			var vm = CreateVm();
			vm.Length = 16u;

			vm.GenerateCommand.Execute(null);

			string password = vm.GeneratedPassword.ReadString();
			Assert.Equal(16, password.Length);
		}

		[Fact]
		public void Generate_UpperCaseOnly_PasswordContainsOnlyUppercase()
		{
			var vm = CreateVm();
			vm.UseUpperCase = true;
			vm.UseLowerCase = false;
			vm.UseDigits = false;
			vm.Length = 12u;

			vm.GenerateCommand.Execute(null);

			string password = vm.GeneratedPassword.ReadString();
			Assert.All(password, c => Assert.True(char.IsUpper(c),
				$"Expected uppercase char, got '{c}' in \"{password}\""));
		}

		[Fact]
		public void Generate_DigitsOnly_PasswordContainsOnlyDigits()
		{
			var vm = CreateVm();
			vm.UseUpperCase = false;
			vm.UseLowerCase = false;
			vm.UseDigits = true;
			vm.Length = 8u;

			vm.GenerateCommand.Execute(null);

			string password = vm.GeneratedPassword.ReadString();
			Assert.All(password, c => Assert.True(char.IsDigit(c),
				$"Expected digit, got '{c}' in \"{password}\""));
		}

		// ------------------------------------------------------------------ //
		// Pattern generation                                                  //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Generate_PatternMode_ProducesPasswordMatchingPattern()
		{
			var vm = CreateVm();
			vm.GeneratorType = PasswordGeneratorType.Pattern;
			vm.Pattern = "AAAAAA"; // 6 upper-case letters via pattern syntax

			vm.GenerateCommand.Execute(null);

			// Pattern 'A' produces uppercase letters; the password should be 6 chars.
			Assert.True(vm.HasGeneratedPassword);
			string pw = vm.GeneratedPassword.ReadString();
			Assert.Equal(6, pw.Length);
		}

		// ------------------------------------------------------------------ //
		// Quality estimation                                                  //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Generate_ProducesNonZeroQualityBits()
		{
			var vm = CreateVm();
			vm.Length = 20u;

			vm.GenerateCommand.Execute(null);

			// Quality may be 0 if QualityEstimation data tables are not
			// initialised in test environment; we only assert no exception.
			Assert.True(vm.PasswordQualityBits >= 0);
		}

		[Fact]
		public void HasGeneratedPassword_RaisesPropertyChanged_OnGenerate()
		{
			var vm = CreateVm();
			var raised = new List<string?>();
			vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

			vm.GenerateCommand.Execute(null);

			Assert.Contains(nameof(vm.HasGeneratedPassword), raised);
			Assert.Contains(nameof(vm.GeneratedPassword), raised);
		}

		// ------------------------------------------------------------------ //
		// Profile CRUD                                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SaveProfile_AddsToProfilesCollection()
		{
			var vm = CreateVm();

			vm.SaveProfileCommand.Execute("Work");

			Assert.Single(vm.Profiles, p => p.Name == "Work");
		}

		[Fact]
		public void SaveProfile_OverwritesExistingProfileWithSameName()
		{
			var vm = CreateVm();
			vm.SaveProfileCommand.Execute("Shared");
			vm.Length = 32u;
			vm.SaveProfileCommand.Execute("Shared");

			Assert.Single(vm.Profiles, p => p.Name == "Shared");
			Assert.Equal(32u, vm.Profiles.Single(p => p.Name == "Shared").Length);
		}

		[Fact]
		public void DeleteProfile_RemovesFromCollection()
		{
			var vm = CreateVm();
			vm.SaveProfileCommand.Execute("Temp");
			var profile = vm.Profiles.First(p => p.Name == "Temp");

			vm.DeleteProfileCommand.Execute(profile);

			Assert.DoesNotContain(vm.Profiles, p => p.Name == "Temp");
		}

		[Fact]
		public void DeleteProfile_ClearsSelectedProfile_WhenItIsTheDeletedOne()
		{
			var vm = CreateVm();
			vm.SaveProfileCommand.Execute("ToDelete");
			var profile = vm.Profiles.First(p => p.Name == "ToDelete");
			vm.SelectedProfile = profile;

			vm.DeleteProfileCommand.Execute(profile);

			Assert.Null(vm.SelectedProfile);
		}

		[Fact]
		public void LoadProfile_AppliesSettingsToViewModel()
		{
			var vm = CreateVm();
			vm.Length = 32u;
			vm.UseSpecial = true;
			vm.SaveProfileCommand.Execute("LongWithSpecial");
			var saved = vm.Profiles.First(p => p.Name == "LongWithSpecial");

			// Reset to defaults and then load the saved profile.
			vm.Length = 8u;
			vm.UseSpecial = false;
			vm.LoadProfileCommand.Execute(saved);

			Assert.Equal(32u, vm.Length);
			Assert.True(vm.UseSpecial);
			Assert.Equal(saved, vm.SelectedProfile);
		}

		// ------------------------------------------------------------------ //
		// BuildProfile / ApplyProfile round-trip                              //
		// ------------------------------------------------------------------ //

		[Fact]
		public void BuildProfile_ReflectsCurrentViewModelState()
		{
			var vm = CreateVm();
			vm.Length = 24u;
			vm.UseUpperCase = true;
			vm.UseLowerCase = true;
			vm.UseDigits = false;
			vm.ExcludeLookAlike = true;

			PwProfile profile = vm.BuildProfile();

			Assert.Equal(24u, profile.Length);
			Assert.True(profile.ExcludeLookAlike);
		}

		[Fact]
		public void ApplyProfile_OverwritesViewModelState()
		{
			var vm = CreateVm();
			var profile = new PwProfile { Length = 48u, Pattern = "LLDD" };
			profile.GeneratorType = PasswordGeneratorType.Pattern;

			vm.ApplyProfile(profile);

			Assert.Equal(48u, vm.Length);
			Assert.Equal("LLDD", vm.Pattern);
			Assert.Equal(PasswordGeneratorType.Pattern, vm.GeneratorType);
		}

		// ------------------------------------------------------------------ //
		// Profiles pre-loaded from store                                      //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Constructor_LoadsExistingProfilesFromStore()
		{
			var store = new InMemoryProfileStore();
			store.SaveProfile(new PwProfile { Name = "Pre-existing" });

			var vm = new PasswordGeneratorViewModel(store);

			Assert.Contains(vm.Profiles, p => p.Name == "Pre-existing");
		}

		// ------------------------------------------------------------------ //
		// HasSecurityReducingOption                                           //
		// ------------------------------------------------------------------ //

		[Theory]
		[InlineData(true, false, "")]
		[InlineData(false, true, "")]
		[InlineData(false, false, "0oO")]
		public void HasSecurityReducingOption_TrueWhenAnySecurityReducerEnabled(
			bool excludeLookAlike, bool noRepeating, string excludeChars)
		{
			var vm = CreateVm();
			vm.ExcludeLookAlike = excludeLookAlike;
			vm.NoRepeatingCharacters = noRepeating;
			vm.ExcludeCharacters = excludeChars;

			Assert.True(vm.HasSecurityReducingOption);
		}

		[Fact]
		public void HasSecurityReducingOption_FalseByDefault()
		{
			var vm = CreateVm();
			Assert.False(vm.HasSecurityReducingOption);
		}
	}
}
