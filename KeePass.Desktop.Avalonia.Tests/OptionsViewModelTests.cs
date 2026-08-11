#nullable enable

using System.Collections.Generic;

using KeePass.Core.ViewModels;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Unit tests for <see cref="OptionsViewModel"/> and its sub-view-models.
	/// </summary>
	public sealed class OptionsViewModelTests
	{
		// ------------------------------------------------------------------ //
		// OptionsViewModel — structure                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Constructor_CreatesAllSubViewModels()
		{
			var vm = new OptionsViewModel();
			Assert.NotNull(vm.Security);
			Assert.NotNull(vm.Interface);
			Assert.NotNull(vm.Integration);
			Assert.NotNull(vm.Advanced);
		}

		[Fact]
		public void ApplyCommand_RaisesAppliedEvent()
		{
			var vm = new OptionsViewModel();
			bool fired = false;
			vm.Applied += (_, _) => fired = true;

			vm.ApplyCommand.Execute(null);

			Assert.True(fired);
		}

		[Fact]
		public void CancelCommand_RaisesCancelledEvent()
		{
			var vm = new OptionsViewModel();
			bool fired = false;
			vm.Cancelled += (_, _) => fired = true;

			vm.CancelCommand.Execute(null);

			Assert.True(fired);
		}

		[Fact]
		public void SecurityLocked_CanBeSetAndRead()
		{
			var vm = new OptionsViewModel();
			vm.SecurityLocked = true;
			Assert.True(vm.SecurityLocked);
		}

		[Fact]
		public void AutoTypeLocked_CanBeSetAndRead()
		{
			var vm = new OptionsViewModel();
			vm.AutoTypeLocked = true;
			Assert.True(vm.AutoTypeLocked);
		}

		// ------------------------------------------------------------------ //
		// SecurityOptionsViewModel                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Security_DefaultLockAfterSeconds_IsZero()
		{
			var vm = new OptionsViewModel();
			Assert.Equal(0u, vm.Security.LockAfterSeconds);
		}

		[Fact]
		public void Security_ClipboardClearAfterSeconds_Default_Is12()
		{
			var vm = new OptionsViewModel();
			Assert.Equal(12, vm.Security.ClipboardClearAfterSeconds);
		}

		[Fact]
		public void Security_ClipboardClearAfterSeconds_NegativeIsClampedToZero()
		{
			var vm = new OptionsViewModel();
			vm.Security.ClipboardClearAfterSeconds = -5;
			Assert.Equal(0, vm.Security.ClipboardClearAfterSeconds);
		}

		[Fact]
		public void Security_MasterKeyTries_NegativeIsClampedToZero()
		{
			var vm = new OptionsViewModel();
			vm.Security.MasterKeyTries = -1;
			Assert.Equal(0, vm.Security.MasterKeyTries);
		}

		[Fact]
		public void Security_LockOnWindowMinimize_DefaultFalse()
		{
			var vm = new OptionsViewModel();
			Assert.False(vm.Security.LockOnWindowMinimize);
		}

		[Fact]
		public void Security_PropertyChanged_RaisedOnLockOnSuspend()
		{
			var vm = new OptionsViewModel();
			var changed = new List<string?>();
			vm.Security.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

			vm.Security.LockOnSuspend = true;

			Assert.Contains(nameof(SecurityOptionsViewModel.LockOnSuspend), changed);
		}

		// ------------------------------------------------------------------ //
		// InterfaceOptionsViewModel                                           //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Interface_ShowToolBar_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Interface.ShowToolBar);
		}

		[Fact]
		public void Interface_ShowStatusBar_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Interface.ShowStatusBar);
		}

		[Fact]
		public void Interface_ExpirySoonDays_Default_Is14()
		{
			var vm = new OptionsViewModel();
			Assert.Equal(14, vm.Interface.ExpirySoonDays);
		}

		[Fact]
		public void Interface_ExpirySoonDays_NegativeIsClampedToZero()
		{
			var vm = new OptionsViewModel();
			vm.Interface.ExpirySoonDays = -3;
			Assert.Equal(0, vm.Interface.ExpirySoonDays);
		}

		[Fact]
		public void Interface_LanguageFile_DefaultIsEmpty()
		{
			var vm = new OptionsViewModel();
			Assert.Equal(string.Empty, vm.Interface.LanguageFile);
		}

		// ------------------------------------------------------------------ //
		// IntegrationOptionsViewModel                                         //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Integration_AutoTypeEnabled_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Integration.AutoTypeEnabled);
		}

		[Fact]
		public void Integration_AutoTypeMatchByTitle_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Integration.AutoTypeMatchByTitle);
		}

		[Fact]
		public void Integration_AutoTypeDelay_Default_Is100()
		{
			var vm = new OptionsViewModel();
			Assert.Equal(100, vm.Integration.AutoTypeDelay);
		}

		[Fact]
		public void Integration_AutoTypeDelay_IsClampedTo30000Max()
		{
			var vm = new OptionsViewModel();
			vm.Integration.AutoTypeDelay = 99999;
			Assert.Equal(30000, vm.Integration.AutoTypeDelay);
		}

		[Fact]
		public void Integration_AutoTypeDelay_IsClampedToZeroMin()
		{
			var vm = new OptionsViewModel();
			vm.Integration.AutoTypeDelay = -1;
			Assert.Equal(0, vm.Integration.AutoTypeDelay);
		}

		[Fact]
		public void Integration_UrlOverride_DefaultIsEmpty()
		{
			var vm = new OptionsViewModel();
			Assert.Equal(string.Empty, vm.Integration.UrlOverride);
		}

		// ------------------------------------------------------------------ //
		// AdvancedOptionsViewModel                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Advanced_OpenLastFile_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Advanced.OpenLastFile);
		}

		[Fact]
		public void Advanced_RememberWorkingDirectories_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Advanced.RememberWorkingDirectories);
		}

		[Fact]
		public void Advanced_UseTransactedFileWrites_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Advanced.UseTransactedFileWrites);
		}

		[Fact]
		public void Advanced_VerifyWrittenFileAfterSaving_DefaultTrue()
		{
			var vm = new OptionsViewModel();
			Assert.True(vm.Advanced.VerifyWrittenFileAfterSaving);
		}

		[Fact]
		public void Advanced_StartMinimized_DefaultFalse()
		{
			var vm = new OptionsViewModel();
			Assert.False(vm.Advanced.StartMinimized);
		}

		// ------------------------------------------------------------------ //
		// PropertyChanged propagation                                         //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Interface_MinimizeToTray_PropertyChangedRaised()
		{
			var vm = new OptionsViewModel();
			var props = new List<string?>();
			vm.Interface.PropertyChanged += (_, e) => props.Add(e.PropertyName);

			vm.Interface.MinimizeToTray = true;

			Assert.Contains(nameof(InterfaceOptionsViewModel.MinimizeToTray), props);
		}

		[Fact]
		public void Integration_AutoTypeEnabled_PropertyChangedRaised()
		{
			var vm = new OptionsViewModel();
			var props = new List<string?>();
			vm.Integration.PropertyChanged += (_, e) => props.Add(e.PropertyName);

			vm.Integration.AutoTypeEnabled = false;

			Assert.Contains(nameof(IntegrationOptionsViewModel.AutoTypeEnabled), props);
		}
	}
}
