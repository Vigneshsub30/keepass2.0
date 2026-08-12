using System;
using System.Collections.Generic;

using KeePass.Core.Models;
using KeePass.Core.ViewModels;

using KeePassLib;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Unit tests for <see cref="GroupEditorViewModel"/> and <see cref="IconPickerViewModel"/>.
	/// </summary>
	public sealed class GroupEditorViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private static PwGroup MakeGroup(string name = "Work",
			bool? autoType = null, bool? searching = null)
		{
			var g = new PwGroup(true, true, name, PwIcon.Folder);
			g.EnableAutoType   = autoType;
			g.EnableSearching  = searching;
			return g;
		}

		// ------------------------------------------------------------------ //
		// Constructor / create mode                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void CreateMode_IsCreateMode_True()
		{
			var vm = new GroupEditorViewModel();
			Assert.True(vm.IsCreateMode);
		}

		[Fact]
		public void CreateMode_DefaultName_IsEmpty()
		{
			var vm = new GroupEditorViewModel();
			Assert.Equal(string.Empty, vm.Name);
		}

		[Fact]
		public void CreateMode_SaveCommand_RequiresName()
		{
			var vm = new GroupEditorViewModel();
			Assert.False(vm.SaveCommand.CanExecute(null));

			vm.Name = "NewGroup";
			Assert.True(vm.SaveCommand.CanExecute(null));
		}

		// ------------------------------------------------------------------ //
		// Edit mode — field population                                         //
		// ------------------------------------------------------------------ //

		[Fact]
		public void EditMode_PopulatesName()
		{
			var vm = new GroupEditorViewModel(MakeGroup("Finance"));
			Assert.Equal("Finance", vm.Name);
			Assert.False(vm.IsCreateMode);
		}

		[Fact]
		public void EditMode_PopulatesIcon()
		{
			var g = MakeGroup();
			g.IconId = PwIcon.Homebanking;
			var vm = new GroupEditorViewModel(g);
			Assert.Equal(PwIcon.Homebanking, vm.IconId);
		}

		[Fact]
		public void EditMode_PopulatesTags()
		{
			var g = MakeGroup();
			g.Tags = new List<string> { "personal", "finance" };
			var vm = new GroupEditorViewModel(g);
			Assert.Contains("personal", vm.Tags);
			Assert.Contains("finance", vm.Tags);
		}

		// ------------------------------------------------------------------ //
		// InheritableBoolean mapping                                           //
		// ------------------------------------------------------------------ //

		[Theory]
		[InlineData(null,  InheritableBoolean.Inherit)]
		[InlineData(true,  InheritableBoolean.Enabled)]
		[InlineData(false, InheritableBoolean.Disabled)]
		public void EnableAutoType_MapsFromNullableBool(bool? input, InheritableBoolean expected)
		{
			var vm = new GroupEditorViewModel(MakeGroup(autoType: input));
			Assert.Equal(expected, vm.EnableAutoType);
		}

		[Theory]
		[InlineData(null,  InheritableBoolean.Inherit)]
		[InlineData(true,  InheritableBoolean.Enabled)]
		[InlineData(false, InheritableBoolean.Disabled)]
		public void EnableSearching_MapsFromNullableBool(bool? input, InheritableBoolean expected)
		{
			var vm = new GroupEditorViewModel(MakeGroup(searching: input));
			Assert.Equal(expected, vm.EnableSearching);
		}

		[Theory]
		[InlineData(InheritableBoolean.Inherit,  null)]
		[InlineData(InheritableBoolean.Enabled,  true)]
		[InlineData(InheritableBoolean.Disabled, false)]
		public void InheritableBoolean_ToNullableBool_RoundTrip(
			InheritableBoolean ib, bool? expected)
		{
			Assert.Equal(expected, ib.ToNullableBool());
		}

		// ------------------------------------------------------------------ //
		// Save — applies changes to source group                               //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Save_EditMode_AppliesNameChange()
		{
			var source = MakeGroup("OldName");
			var vm     = new GroupEditorViewModel(source);
			vm.Name    = "NewName";

			vm.SaveCommand.Execute(null);

			Assert.Equal("NewName", source.Name);
		}

		[Fact]
		public void Save_EditMode_AppliesAutoTypeSetting()
		{
			var source = MakeGroup();
			var vm     = new GroupEditorViewModel(source);
			vm.EnableAutoType = InheritableBoolean.Disabled;

			vm.SaveCommand.Execute(null);

			Assert.Equal(false, source.EnableAutoType);
		}

		[Fact]
		public void Save_EditMode_AppliesSearchingSetting()
		{
			var source = MakeGroup();
			var vm     = new GroupEditorViewModel(source);
			vm.EnableSearching = InheritableBoolean.Enabled;

			vm.SaveCommand.Execute(null);

			Assert.Equal(true, source.EnableSearching);
		}

		[Fact]
		public void Save_RaisesSavedEvent()
		{
			var vm   = new GroupEditorViewModel();
			vm.Name  = "Test";
			bool saved = false;
			vm.Saved += (_, _) => saved = true;

			vm.SaveCommand.Execute(null);

			Assert.True(saved);
		}

		// ------------------------------------------------------------------ //
		// Cancel                                                               //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Cancel_DoesNotModifySource()
		{
			var source = MakeGroup("Original");
			var vm     = new GroupEditorViewModel(source);
			vm.Name    = "Modified";

			bool cancelled = false;
			vm.Cancelled += (_, _) => cancelled = true;

			vm.CancelCommand.Execute(null);

			Assert.True(cancelled);
			Assert.Equal("Original", source.Name);
		}

		// ------------------------------------------------------------------ //
		// IconPickerViewModel                                                   //
		// ------------------------------------------------------------------ //

		[Fact]
		public void IconPicker_LoadsAllStandardIcons()
		{
			var vm = new IconPickerViewModel();
			Assert.Equal((int)PwIcon.Count, vm.Icons.Count);
		}

		[Fact]
		public void IconPicker_PreSelectsCurrentIcon()
		{
			var vm = new IconPickerViewModel(PwIcon.Homebanking);
			var selected = System.Linq.Enumerable.FirstOrDefault(vm.Icons, i => i.IsSelected);
			Assert.NotNull(selected);
			Assert.Equal(PwIcon.Homebanking, selected!.IconId);
		}

		[Fact]
		public void IconPicker_SelectIconCommand_UpdatesSelection()
		{
			var vm   = new IconPickerViewModel(PwIcon.Key);
			var item = System.Linq.Enumerable.FirstOrDefault(vm.Icons, i => i.IconId == PwIcon.Folder);
			Assert.NotNull(item);

			vm.SelectIconCommand.Execute(item);

			Assert.Equal(PwIcon.Folder, vm.SelectedIconId);
			Assert.True(item!.IsSelected);
		}

		[Fact]
		public void IconPicker_ConfirmCommand_RaisesSelectionConfirmed()
		{
			var vm    = new IconPickerViewModel();
			bool confirmed = false;
			vm.SelectionConfirmed += (_, _) => confirmed = true;

			vm.ConfirmCommand.Execute(null);

			Assert.True(confirmed);
		}

		[Fact]
		public void IconPicker_CancelCommand_RaisesSelectionCancelled()
		{
			var vm        = new IconPickerViewModel();
			bool cancelled = false;
			vm.SelectionCancelled += (_, _) => cancelled = true;

			vm.CancelCommand.Execute(null);

			Assert.True(cancelled);
		}

		// ------------------------------------------------------------------ //
		// No WinForms reference                                                //
		// ------------------------------------------------------------------ //

		[Fact]
		public void GroupEditorViewModel_Assembly_HasNoWinFormsReference()
		{
			var asm = typeof(GroupEditorViewModel).Assembly;
			bool hasWinForms = System.Linq.Enumerable.Any(
				asm.GetReferencedAssemblies(),
				r => r.Name != null &&
					r.Name.StartsWith("System.Windows.Forms", StringComparison.Ordinal));
			Assert.False(hasWinForms);
		}
	}
}
