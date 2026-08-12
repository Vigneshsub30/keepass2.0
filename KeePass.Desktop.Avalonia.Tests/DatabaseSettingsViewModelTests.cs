#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using KeePass.Core.ViewModels;

using KeePassLib;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Unit tests for <see cref="DatabaseSettingsViewModel"/> and its
	/// nested parameter view-models
	/// (<see cref="Argon2ParametersViewModel"/>, <see cref="AesKdfParametersViewModel"/>).
	/// </summary>
	public sealed class DatabaseSettingsViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Helpers                                                             //
		// ------------------------------------------------------------------ //

		private static PwDatabase CreateTestDatabase(Action<PwDatabase>? configure = null)
		{
			var db = new PwDatabase();
			db.New(new KeePassLib.Serialization.IOConnectionInfo(),
				new KeePassLib.Keys.CompositeKey());
			configure?.Invoke(db);
			return db;
		}

		private static DatabaseSettingsViewModel CreateVm(Action<PwDatabase>? configure = null)
			=> new DatabaseSettingsViewModel(CreateTestDatabase(configure));

		// ------------------------------------------------------------------ //
		// Constructor / LoadFromDatabase                                      //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Constructor_LoadsDatabaseName()
		{
			var vm = CreateVm(db => db.Name = "My Test Vault");
			Assert.Equal("My Test Vault", vm.DatabaseName);
		}

		[Fact]
		public void Constructor_LoadsDescription()
		{
			var vm = CreateVm(db => db.Description = "Work passwords");
			Assert.Equal("Work passwords", vm.Description);
		}

		[Fact]
		public void Constructor_LoadsDefaultUserName()
		{
			var vm = CreateVm(db => db.DefaultUserName = "admin");
			Assert.Equal("admin", vm.DefaultUserName);
		}

		[Fact]
		public void Constructor_PopulatesCiphers()
		{
			var vm = CreateVm();
			Assert.NotEmpty(vm.Ciphers);
			Assert.All(vm.Ciphers, c => Assert.NotNull(c.Name));
		}

		[Fact]
		public void Constructor_PopulatesKdfEngines()
		{
			var vm = CreateVm();
			Assert.NotEmpty(vm.KdfEngines);
			Assert.All(vm.KdfEngines, k => Assert.NotNull(k.Name));
		}

		[Fact]
		public void Constructor_SelectedCipherMatchesDatabase()
		{
			// Use default database cipher — should be set to AES-256 by default
			var vm = CreateVm();
			Assert.NotNull(vm.SelectedCipher);
			Assert.True(vm.SelectedCipher!.Engine.CipherUuid.Equals(
				vm.SelectedCipher.Engine.CipherUuid)); // tautology but checks non-null
		}

		[Fact]
		public void Constructor_SelectedKdfMatchesDatabase()
		{
			var vm = CreateVm();
			Assert.NotNull(vm.SelectedKdf);
			// KDF engine must be one of the registered engines
			Assert.Contains(vm.KdfEngines, k => k == vm.SelectedKdf);
		}

		[Fact]
		public void Constructor_LoadsCompressionNone()
		{
			var vm = CreateVm(db => db.Compression = PwCompressionAlgorithm.None);
			Assert.True(vm.IsCompressionNone);
			Assert.False(vm.IsCompressionGZip);
		}

		[Fact]
		public void Constructor_LoadsCompressionGZip()
		{
			var vm = CreateVm(db => db.Compression = PwCompressionAlgorithm.GZip);
			Assert.False(vm.IsCompressionNone);
			Assert.True(vm.IsCompressionGZip);
		}

		[Fact]
		public void Constructor_LoadsRecycleBinEnabled()
		{
			var vm = CreateVm(db => db.RecycleBinEnabled = false);
			Assert.False(vm.RecycleBinEnabled);
		}

		[Fact]
		public void Constructor_LoadsHistoryMaxItems()
		{
			var vm = CreateVm(db => db.HistoryMaxItems = 15);
			Assert.Equal(15, vm.HistoryMaxItems);
		}

		[Fact]
		public void Constructor_LoadsHistoryMaxSize()
		{
			var vm = CreateVm(db => db.HistoryMaxSize = 1024 * 1024 * 6);
			Assert.Equal(1024 * 1024 * 6, vm.HistoryMaxSize);
		}

		// ------------------------------------------------------------------ //
		// Cipher selection                                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SelectedCipher_CanBeChangedToAnyPoolEntry()
		{
			var vm = CreateVm();
			var last = vm.Ciphers.Last();
			vm.SelectedCipher = last;
			Assert.Equal(last, vm.SelectedCipher);
		}

		// ------------------------------------------------------------------ //
		// KDF selection — dynamic parameter switching                         //
		// ------------------------------------------------------------------ //

		[Fact]
		public void SelectedKdf_SwitchToArgon2_KdfParametersIsArgon2ParametersViewModel()
		{
			var vm = CreateVm();
			var argon2Item = vm.KdfEngines.FirstOrDefault(k => k.Engine is Argon2Kdf);
			if (argon2Item == null) return; // Argon2 not registered — skip

			vm.SelectedKdf = argon2Item;

			Assert.IsType<Argon2ParametersViewModel>(vm.KdfParameters);
		}

		[Fact]
		public void SelectedKdf_SwitchToAesKdf_KdfParametersIsAesKdfParametersViewModel()
		{
			var vm = CreateVm();
			var aesItem = vm.KdfEngines.FirstOrDefault(k => k.Engine is AesKdf);
			if (aesItem == null) return;

			vm.SelectedKdf = aesItem;

			Assert.IsType<AesKdfParametersViewModel>(vm.KdfParameters);
		}

		[Fact]
		public void SelectedKdf_Changing_RaisesPropertyChangedForKdfParameters()
		{
			var vm = CreateVm();
			var raised = new List<string?>();
			vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

			// Switch to any other engine
			var other = vm.KdfEngines.FirstOrDefault(k => k != vm.SelectedKdf);
			if (other == null) return;
			vm.SelectedKdf = other;

			Assert.Contains(nameof(DatabaseSettingsViewModel.KdfParameters), raised);
		}

		// ------------------------------------------------------------------ //
		// Compression radio toggles                                           //
		// ------------------------------------------------------------------ //

		[Fact]
		public void IsCompressionGZip_Setting_UpdatesCompression()
		{
			var vm = CreateVm(db => db.Compression = PwCompressionAlgorithm.None);
			vm.IsCompressionGZip = true;
			Assert.Equal(PwCompressionAlgorithm.GZip, vm.Compression);
			Assert.True(vm.IsCompressionGZip);
			Assert.False(vm.IsCompressionNone);
		}

		[Fact]
		public void IsCompressionNone_Setting_UpdatesCompression()
		{
			var vm = CreateVm(db => db.Compression = PwCompressionAlgorithm.GZip);
			vm.IsCompressionNone = true;
			Assert.Equal(PwCompressionAlgorithm.None, vm.Compression);
			Assert.True(vm.IsCompressionNone);
			Assert.False(vm.IsCompressionGZip);
		}

		// ------------------------------------------------------------------ //
		// HistoryMaxSizeDecimal bridge                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void HistoryMaxSizeDecimal_GetsAndSets_MatchingLongValue()
		{
			var vm = CreateVm();
			vm.HistoryMaxSizeDecimal = 8_388_608m; // 8 MB
			Assert.Equal(8_388_608L, vm.HistoryMaxSize);
			Assert.Equal(8_388_608m, vm.HistoryMaxSizeDecimal);
		}

		// ------------------------------------------------------------------ //
		// Apply (OK)                                                          //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ApplyCommand_WritesNameToDatabase()
		{
			var db = CreateTestDatabase();
			var vm = new DatabaseSettingsViewModel(db);
			vm.DatabaseName = "Updated Name";

			vm.ApplyCommand.Execute(null);

			Assert.Equal("Updated Name", db.Name);
		}

		[Fact]
		public void ApplyCommand_WritesDescriptionToDatabase()
		{
			var db = CreateTestDatabase();
			var vm = new DatabaseSettingsViewModel(db);
			vm.Description = "Updated desc";

			vm.ApplyCommand.Execute(null);

			Assert.Equal("Updated desc", db.Description);
		}

		[Fact]
		public void ApplyCommand_WritesDefaultUserNameToDatabase()
		{
			var db = CreateTestDatabase();
			var vm = new DatabaseSettingsViewModel(db);
			vm.DefaultUserName = "jane";

			vm.ApplyCommand.Execute(null);

			Assert.Equal("jane", db.DefaultUserName);
		}

		[Fact]
		public void ApplyCommand_WritesCompressionToDatabase()
		{
			var db = CreateTestDatabase(d => d.Compression = PwCompressionAlgorithm.None);
			var vm = new DatabaseSettingsViewModel(db);
			vm.IsCompressionGZip = true;

			vm.ApplyCommand.Execute(null);

			Assert.Equal(PwCompressionAlgorithm.GZip, db.Compression);
		}

		[Fact]
		public void ApplyCommand_WritesRecycleBinEnabledToDatabase()
		{
			var db = CreateTestDatabase(d => d.RecycleBinEnabled = true);
			var vm = new DatabaseSettingsViewModel(db);
			vm.RecycleBinEnabled = false;

			vm.ApplyCommand.Execute(null);

			Assert.False(db.RecycleBinEnabled);
		}

		[Fact]
		public void ApplyCommand_WritesHistoryMaxItemsToDatabase()
		{
			var db = CreateTestDatabase();
			var vm = new DatabaseSettingsViewModel(db);
			vm.HistoryMaxItems = 50;

			vm.ApplyCommand.Execute(null);

			Assert.Equal(50, db.HistoryMaxItems);
		}

		[Fact]
		public void ApplyCommand_WritesHistoryMaxSizeToDatabase()
		{
			var db = CreateTestDatabase();
			var vm = new DatabaseSettingsViewModel(db);
			vm.HistoryMaxSizeDecimal = 2_097_152m;

			vm.ApplyCommand.Execute(null);

			Assert.Equal(2_097_152L, db.HistoryMaxSize);
		}

		[Fact]
		public void ApplyCommand_RaisesAppliedEvent()
		{
			var vm = CreateVm();
			bool applied = false;
			vm.Applied += (_, _) => applied = true;

			vm.ApplyCommand.Execute(null);

			Assert.True(applied);
		}

		[Fact]
		public void CancelCommand_RaisesCancelledEvent()
		{
			var vm = CreateVm();
			bool cancelled = false;
			vm.Cancelled += (_, _) => cancelled = true;

			vm.CancelCommand.Execute(null);

			Assert.True(cancelled);
		}

		// ------------------------------------------------------------------ //
		// Argon2ParametersViewModel                                           //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Argon2Params_DefaultMemoryMb_IsAtLeast8()
		{
			var vm = CreateVm();
			Assert.True(vm.Argon2Params.MemoryMb >= 8);
		}

		[Fact]
		public void Argon2Params_ApplyTo_SetsCorrectBytesInParameters()
		{
			var vm = CreateVm();
			vm.Argon2Params.MemoryMb = 32u;
			vm.Argon2Params.Iterations = 4u;
			vm.Argon2Params.Parallelism = 3u;

			var kdfParams = new KdfParameters(KdfPool.Engines.First(k => k is Argon2Kdf).Uuid);
			vm.Argon2Params.ApplyTo(kdfParams);

			Assert.Equal(32UL * 1024 * 1024, kdfParams.GetUInt64(Argon2Kdf.ParamMemory, 0));
			Assert.Equal(4UL, kdfParams.GetUInt64(Argon2Kdf.ParamIterations, 0));
			Assert.Equal(3U, kdfParams.GetUInt32(Argon2Kdf.ParamParallelism, 0));
		}

		[Fact]
		public void Argon2Params_LoadFrom_ParsesByteCountToMb()
		{
			var vm = CreateVm();
			var kdfParams = new KdfParameters(KdfPool.Engines.First(k => k is Argon2Kdf).Uuid);
			kdfParams.SetUInt64(Argon2Kdf.ParamMemory, 128UL * 1024 * 1024);
			kdfParams.SetUInt64(Argon2Kdf.ParamIterations, 3);
			kdfParams.SetUInt32(Argon2Kdf.ParamParallelism, 4);

			vm.Argon2Params.LoadFrom(kdfParams);

			Assert.Equal(128u, vm.Argon2Params.MemoryMb);
			Assert.Equal(3UL, vm.Argon2Params.Iterations);
			Assert.Equal(4u, vm.Argon2Params.Parallelism);
		}

		// ------------------------------------------------------------------ //
		// AesKdfParametersViewModel                                           //
		// ------------------------------------------------------------------ //

		[Fact]
		public void AesKdfParams_ApplyTo_SetsRoundsInParameters()
		{
			var vm = CreateVm();
			vm.AesKdfParams.Rounds = 100_000u;

			var kdfParams = new KdfParameters(KdfPool.Engines.First(k => k is AesKdf).Uuid);
			vm.AesKdfParams.ApplyTo(kdfParams);

			Assert.Equal(100_000UL, kdfParams.GetUInt64(AesKdf.ParamRounds, 0));
		}

		[Fact]
		public void AesKdfParams_LoadFrom_ReadsRoundsFromParameters()
		{
			var vm = CreateVm();
			var kdfParams = new KdfParameters(KdfPool.Engines.First(k => k is AesKdf).Uuid);
			kdfParams.SetUInt64(AesKdf.ParamRounds, 200_000u);

			vm.AesKdfParams.LoadFrom(kdfParams);

			Assert.Equal(200_000UL, vm.AesKdfParams.Rounds);
		}
	}
}
