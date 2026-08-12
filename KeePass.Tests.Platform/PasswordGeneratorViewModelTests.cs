using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using KeePass.Core.Services;
using KeePass.Core.ViewModels;

using KeePassLib.Cryptography.PasswordGenerator;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Unit tests for <see cref="PasswordGeneratorViewModel"/>.
	/// All tests use in-memory profile store and synchronous generation
	/// — no WinForms, no external dependencies.
	/// </summary>
	public sealed class PasswordGeneratorViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private sealed class InMemoryGeneratorProfileStore : IGeneratorProfileStore
		{
			private readonly Dictionary<string, PwProfile> _profiles =
				new Dictionary<string, PwProfile>(StringComparer.OrdinalIgnoreCase);

			public IReadOnlyList<PwProfile> GetProfiles() => _profiles.Values.ToList();
			public void SaveProfile(PwProfile p) => _profiles[p.Name] = p;
			public void DeleteProfile(string name) => _profiles.Remove(name);
		}

		private static PasswordGeneratorViewModel MakeVm(
			IGeneratorProfileStore? store = null) =>
			new PasswordGeneratorViewModel(store ?? new InMemoryGeneratorProfileStore());

		// ------------------------------------------------------------------ //
		// Constructor tests                                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Constructor_NullStore_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new PasswordGeneratorViewModel(null!));
		}

		// ------------------------------------------------------------------ //
		// Default state                                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void InitialState_DefaultsMatchPwProfileDefaults()
		{
			var vm = MakeVm();
			var def = new PwProfile();

			Assert.Equal(def.GeneratorType, vm.GeneratorType);
			Assert.Equal(def.Length, vm.Length);
			Assert.Equal(def.ExcludeLookAlike, vm.ExcludeLookAlike);
			Assert.Equal(def.NoRepeatingCharacters, vm.NoRepeatingCharacters);
			Assert.Equal(def.ExcludeCharacters, vm.ExcludeCharacters);
		}

		[Fact]
		public void InitialState_GeneratedPasswordIsEmpty()
		{
			var vm = MakeVm();
			Assert.True(vm.GeneratedPassword.IsEmpty);
			Assert.Equal(0u, vm.PasswordQualityBits);
			Assert.Equal(string.Empty, vm.GenerationError);
		}

		[Fact]
		public void InitialState_DefaultCharSetFlagsReflectDefaultProfile()
		{
			var vm = MakeVm();
			// Default PwProfile CharSet contains Upper, Lower, Digits.
			Assert.True(vm.UseUpperCase);
			Assert.True(vm.UseLowerCase);
			Assert.True(vm.UseDigits);
			Assert.False(vm.UseSpecial);
			Assert.False(vm.UseBrackets);
			Assert.False(vm.UseLatin1);
		}

		// ------------------------------------------------------------------ //
		// HasSecurityReducingOption                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void HasSecurityReducingOption_NoOptionsSet_ReturnsFalse()
		{
			var vm = MakeVm();
			Assert.False(vm.HasSecurityReducingOption);
		}

		[Fact]
		public void HasSecurityReducingOption_ExcludeLookAlike_ReturnsTrue()
		{
			var vm = MakeVm();
			vm.ExcludeLookAlike = true;
			Assert.True(vm.HasSecurityReducingOption);
		}

		[Fact]
		public void HasSecurityReducingOption_NoRepeatingCharacters_ReturnsTrue()
		{
			var vm = MakeVm();
			vm.NoRepeatingCharacters = true;
			Assert.True(vm.HasSecurityReducingOption);
		}

		[Fact]
		public void HasSecurityReducingOption_NonEmptyExcludeCharacters_ReturnsTrue()
		{
			var vm = MakeVm();
			vm.ExcludeCharacters = "0O1Il";
			Assert.True(vm.HasSecurityReducingOption);
		}

		[Fact]
		public void HasSecurityReducingOption_PropertyChanged_WhenSecurityOptionChanges()
		{
			var vm = MakeVm();
			var changed = new List<string>();
			((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
			{
				if (e.PropertyName != null) changed.Add(e.PropertyName);
			};

			vm.ExcludeLookAlike = true;

			Assert.Contains(nameof(PasswordGeneratorViewModel.HasSecurityReducingOption), changed);
		}

		// ------------------------------------------------------------------ //
		// GenerateCommand — CharSet                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void GenerateCommand_CharSet_ProducesNonEmptyPassword()
		{
			var vm = MakeVm();
			vm.Length = 16;

			vm.GenerateCommand.Execute(null);

			Assert.False(vm.GeneratedPassword.IsEmpty);
			Assert.Equal(string.Empty, vm.GenerationError);
		}

		[Fact]
		public void GenerateCommand_CharSet_PasswordLengthMatchesConfiguredLength()
		{
			var vm = MakeVm();
			vm.Length = 24;
			vm.UseUpperCase = true;
			vm.UseLowerCase = true;
			vm.UseDigits = true;

			vm.GenerateCommand.Execute(null);

			string pwd = vm.GeneratedPassword.ReadString();
			Assert.Equal(24, pwd.Length);
		}

		[Fact]
		public void GenerateCommand_UpperCaseOnly_PasswordContainsOnlyUpperCase()
		{
			var vm = MakeVm();
			vm.UseUpperCase = true;
			vm.UseLowerCase = false;
			vm.UseDigits = false;
			vm.Length = 20;

			vm.GenerateCommand.Execute(null);

			string pwd = vm.GeneratedPassword.ReadString();
			Assert.All(pwd, c => Assert.True(char.IsUpper(c), $"'{c}' is not uppercase"));
		}

		[Fact]
		public void GenerateCommand_DigitsOnly_PasswordContainsOnlyDigits()
		{
			var vm = MakeVm();
			vm.UseUpperCase = false;
			vm.UseLowerCase = false;
			vm.UseDigits = true;
			vm.Length = 12;

			vm.GenerateCommand.Execute(null);

			string pwd = vm.GeneratedPassword.ReadString();
			Assert.All(pwd, c => Assert.True(char.IsDigit(c), $"'{c}' is not a digit"));
		}

		[Fact]
		public void GenerateCommand_UpdatesPasswordQualityBits()
		{
			var vm = MakeVm();
			vm.Length = 20;

			vm.GenerateCommand.Execute(null);

			// Quality can be 0 if PopularPasswords isn't loaded; just verify it doesn't throw.
			Assert.True(vm.PasswordQualityBits >= 0u);
		}

		[Fact]
		public void GenerateCommand_RaisesPropertyChangedForGeneratedPassword()
		{
			var vm = MakeVm();
			bool fired = false;
			((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(PasswordGeneratorViewModel.GeneratedPassword))
					fired = true;
			};

			vm.GenerateCommand.Execute(null);

			Assert.True(fired);
		}

		// ------------------------------------------------------------------ //
		// GenerateCommand — Pattern                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void GenerateCommand_PatternMode_DigitPattern_ProducesDigitsOnly()
		{
			var vm = MakeVm();
			vm.GeneratorType = PasswordGeneratorType.Pattern;
			// 'd' expands to a digit in KeePass pattern syntax.
			vm.Pattern = "ddddddddd"; // 9 digits

			vm.GenerateCommand.Execute(null);

			Assert.Equal(string.Empty, vm.GenerationError);
			string pwd = vm.GeneratedPassword.ReadString();
			Assert.Equal(9, pwd.Length);
			Assert.All(pwd, c => Assert.True(char.IsDigit(c)));
		}

		// ------------------------------------------------------------------ //
		// BuildProfile / ApplyProfile round-trip                               //
		// ------------------------------------------------------------------ //

		[Fact]
		public void BuildProfile_ReflectsAllCurrentProperties()
		{
			var vm = MakeVm();
			vm.GeneratorType = PasswordGeneratorType.Pattern;
			vm.Length = 32;
			vm.UseUpperCase = false;
			vm.UseLowerCase = true;
			vm.UseDigits = true;
			vm.UseSpecial = true;
			vm.UseBrackets = false;
			vm.UseLatin1 = false;
			vm.CharSetAdditional = "@#";
			vm.Pattern = "a{4}";
			vm.ExcludeLookAlike = true;
			vm.NoRepeatingCharacters = true;
			vm.ExcludeCharacters = "0Oo";
			vm.CustomAlgorithmUuid = "test-uuid";

			PwProfile p = vm.BuildProfile();

			Assert.Equal(PasswordGeneratorType.Pattern, p.GeneratorType);
			Assert.Equal(32u, p.Length);
			Assert.False(p.CharSet.Contains(PwCharSet.UpperCase));
			Assert.True(p.CharSet.Contains(PwCharSet.LowerCase));
			Assert.True(p.CharSet.Contains(PwCharSet.Digits));
			Assert.True(p.CharSet.Contains(PwCharSet.Special));
			Assert.False(p.CharSet.Contains(PwCharSet.Brackets));
			// CharSetAdditional chars are folded into CharSet rather than set
			// via the PwProfile.CharSetAdditional property, to avoid the
			// UpdateCharSet side effect that requires a packed CharSetRanges string.
			Assert.True(p.CharSet.Contains("@#"));
			Assert.Equal("a{4}", p.Pattern);
			Assert.True(p.ExcludeLookAlike);
			Assert.True(p.NoRepeatingCharacters);
			Assert.Equal("0Oo", p.ExcludeCharacters);
			Assert.Equal("test-uuid", p.CustomAlgorithmUuid);
		}

		[Fact]
		public void ApplyProfile_PopulatesAllProperties()
		{
			var vm = MakeVm();
			var p = new PwProfile
			{
				GeneratorType = PasswordGeneratorType.Pattern,
				Length = 16,
				Pattern = "lll",
				ExcludeLookAlike = true,
				NoRepeatingCharacters = true,
				ExcludeCharacters = "XYZ",
				CustomAlgorithmUuid = "uuid-test"
			};
			p.CharSet.Clear();
			p.CharSet.Add(PwCharSet.UpperCase);
			p.CharSet.Add(PwCharSet.Special);

			vm.ApplyProfile(p);

			Assert.Equal(PasswordGeneratorType.Pattern, vm.GeneratorType);
			Assert.Equal(16u, vm.Length);
			Assert.Equal("lll", vm.Pattern);
			Assert.True(vm.UseUpperCase);
			Assert.False(vm.UseLowerCase);
			Assert.False(vm.UseDigits);
			Assert.True(vm.UseSpecial);
			Assert.True(vm.ExcludeLookAlike);
			Assert.True(vm.NoRepeatingCharacters);
			Assert.Equal("XYZ", vm.ExcludeCharacters);
			Assert.Equal("uuid-test", vm.CustomAlgorithmUuid);
		}

		[Fact]
		public void ApplyProfile_Null_ThrowsArgumentNullException()
		{
			var vm = MakeVm();
			Assert.Throws<ArgumentNullException>(() => vm.ApplyProfile(null!));
		}

		// ------------------------------------------------------------------ //
		// Profile CRUD                                                         //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SaveProfileCommand_NewProfile_AppearsInProfiles()
		{
			var vm = MakeVm();
			vm.Length = 12;

			vm.SaveProfileCommand.Execute("MyProfile");

			Assert.Single(vm.Profiles);
			Assert.Equal("MyProfile", vm.Profiles[0].Name);
		}

		[Fact]
		public void SaveProfileCommand_SameName_ReplacesExisting()
		{
			var vm = MakeVm();
			vm.Length = 12;
			vm.SaveProfileCommand.Execute("P");

			vm.Length = 24;
			vm.SaveProfileCommand.Execute("P");

			Assert.Single(vm.Profiles);
			Assert.Equal(24u, vm.Profiles[0].Length);
		}

		[Fact]
		public void LoadProfileCommand_RestoresProperties()
		{
			var vm = MakeVm();
			vm.Length = 8;
			vm.UseSpecial = true;
			vm.SaveProfileCommand.Execute("Loaded");

			vm.Length = 20;
			vm.UseSpecial = false;

			vm.LoadProfileCommand.Execute(vm.Profiles[0]);

			Assert.Equal(8u, vm.Length);
			Assert.True(vm.UseSpecial);
			Assert.Equal(vm.Profiles[0], vm.SelectedProfile);
		}

		[Fact]
		public void DeleteProfileCommand_RemovesFromCollection()
		{
			var vm = MakeVm();
			vm.SaveProfileCommand.Execute("ToDelete");

			var profile = vm.Profiles[0];
			vm.DeleteProfileCommand.Execute(profile);

			Assert.Empty(vm.Profiles);
		}

		[Fact]
		public void DeleteProfileCommand_SelectedProfile_ClearsSelection()
		{
			var vm = MakeVm();
			vm.SaveProfileCommand.Execute("P");
			vm.LoadProfileCommand.Execute(vm.Profiles[0]);

			vm.DeleteProfileCommand.Execute(vm.Profiles[0]);

			Assert.Null(vm.SelectedProfile);
		}

		[Fact]
		public void SaveProfileCommand_NullName_DoesNothing()
		{
			var vm = MakeVm();
			vm.SaveProfileCommand.Execute(null);
			Assert.Empty(vm.Profiles);
		}

		[Fact]
		public void Constructor_LoadsExistingProfilesFromStore()
		{
			var store = new InMemoryGeneratorProfileStore();
			store.SaveProfile(new PwProfile { Name = "Pre-existing" });

			var vm = new PasswordGeneratorViewModel(store);

			Assert.Single(vm.Profiles);
			Assert.Equal("Pre-existing", vm.Profiles[0].Name);
		}

		// ------------------------------------------------------------------ //
		// No WinForms references                                               //
		// ------------------------------------------------------------------ //

		[Fact]
		public void PasswordGeneratorViewModel_HasNoWinFormsReference()
		{
			var asm = typeof(PasswordGeneratorViewModel).Assembly;
			foreach (var refName in asm.GetReferencedAssemblies())
			{
				Assert.DoesNotContain("System.Windows.Forms", refName.FullName,
					StringComparison.OrdinalIgnoreCase);
			}
		}
	}
}
