using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KeePass.Core.Projections;
using KeePass.Core.ViewModels;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Integration tests for <see cref="EntryEditorViewModel"/> exercised
	/// through the test context (no real Avalonia window needed).
	/// </summary>
	public sealed class EntryEditorViewModelTests
	{
		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private static PwEntry MakeFullEntry()
		{
			var e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField,    new ProtectedString(false, "Gmail"));
			e.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "user@gmail.com"));
			e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true,  "secret!"));
			e.Strings.Set(PwDefs.UrlField,      new ProtectedString(false, "https://gmail.com"));
			e.Strings.Set(PwDefs.NotesField,    new ProtectedString(false, "Main account"));
			e.Strings.Set("TOTP",               new ProtectedString(true,  "123456"));
			e.Tags.Add("email");
			e.Tags.Add("work");
			return e;
		}

		// ------------------------------------------------------------------ //
		// Constructor / create mode                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Constructor_NullMapper_Throws()
		{
			var e = new PwEntry(true, true);
			Assert.Throws<ArgumentNullException>(() =>
				new EntryEditorViewModel(e, null!));
		}

		[Fact]
		public void CreateMode_NullEntry_IsCreateMode_True()
		{
			var vm = new EntryEditorViewModel(null);
			Assert.True(vm.IsCreateMode);
		}

		[Fact]
		public void EditMode_SetsStandardFields()
		{
			var vm = new EntryEditorViewModel(MakeFullEntry());

			Assert.Equal("Gmail", vm.Title);
			Assert.Equal("user@gmail.com", vm.UserName);
			Assert.Equal("https://gmail.com", vm.Url);
			Assert.Equal("Main account", vm.Notes);
			Assert.False(vm.IsCreateMode);
		}

		// ------------------------------------------------------------------ //
		// Custom fields tab                                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void EditMode_CustomFields_Populated()
		{
			var vm = new EntryEditorViewModel(MakeFullEntry());

			Assert.Single(vm.CustomFields);
			Assert.Equal("TOTP", vm.CustomFields[0].Name);
		}

		[Fact]
		public void AddField_IncreasesCustomFieldsCount()
		{
			var vm = new EntryEditorViewModel(null);
			int before = vm.CustomFields.Count;

			vm.AddFieldCommand.Execute("MyField");

			Assert.Equal(before + 1, vm.CustomFields.Count);
			Assert.Equal("MyField", vm.CustomFields[before].Name);
		}

		[Fact]
		public void RemoveField_DecreasesCustomFieldsCount()
		{
			var vm = new EntryEditorViewModel(null);
			vm.AddFieldCommand.Execute("X");
			var fvm = vm.CustomFields[0];

			vm.RemoveFieldCommand.Execute(fvm);

			Assert.Empty(vm.CustomFields);
		}

		// ------------------------------------------------------------------ //
		// Tags                                                                 //
		// ------------------------------------------------------------------ //

		[Fact]
		public void EditMode_Tags_Populated()
		{
			var vm = new EntryEditorViewModel(MakeFullEntry());

			Assert.Contains("email", vm.Tags);
			Assert.Contains("work", vm.Tags);
		}

		// ------------------------------------------------------------------ //
		// History tab                                                          //
		// ------------------------------------------------------------------ //

		[Fact]
		public void EditMode_HistoryEntries_FromSource()
		{
			var e = MakeFullEntry();
			// Create a backup (history snapshot) manually.
			e.CreateBackup(null);

			var vm = new EntryEditorViewModel(e);

			Assert.NotEmpty(vm.HistoryEntries);
		}

		// ------------------------------------------------------------------ //
		// Attachments tab                                                       //
		// ------------------------------------------------------------------ //

		[Fact]
		public void AddAttachment_AppearsInCollection()
		{
			var vm = new EntryEditorViewModel(null);
			var data = new AttachmentData { Name = "readme.txt", Content = new byte[] { 1, 2, 3 } };

			vm.AddAttachmentCommand.Execute(data);

			Assert.Single(vm.Attachments);
			Assert.Equal("readme.txt", vm.Attachments[0].Name);
		}

		[Fact]
		public void RemoveAttachment_RemovesFromCollection()
		{
			var vm = new EntryEditorViewModel(null);
			vm.AddAttachmentCommand.Execute(
				new AttachmentData { Name = "file.bin", Content = new byte[] { 9 } });
			var binRef = vm.Attachments[0];

			vm.RemoveAttachmentCommand.Execute(binRef);

			Assert.Empty(vm.Attachments);
		}

		// ------------------------------------------------------------------ //
		// Auto-type tab                                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void AddAssociation_IncreasesCount()
		{
			var vm = new EntryEditorViewModel(null);
			vm.AddAssociationCommand.Execute(null);

			Assert.Single(vm.AutoTypeAssociations);
		}

		[Fact]
		public void RemoveAssociation_DecreasesCount()
		{
			var vm = new EntryEditorViewModel(null);
			vm.AddAssociationCommand.Execute(null);
			var assoc = vm.AutoTypeAssociations[0];

			vm.RemoveAssociationCommand.Execute(assoc);

			Assert.Empty(vm.AutoTypeAssociations);
		}

		// ------------------------------------------------------------------ //
		// Password validation                                                  //
		// ------------------------------------------------------------------ //

		[Fact]
		public void PasswordMismatch_WhenPasswordsDiffer_IsTrue()
		{
			var vm = new EntryEditorViewModel(null);
			vm.Password       = new ProtectedString(true, "abc");
			vm.PasswordRepeat = new ProtectedString(true, "xyz");

			Assert.True(vm.PasswordMismatch);
		}

		[Fact]
		public void PasswordMismatch_WhenPasswordsMatch_IsFalse()
		{
			var vm = new EntryEditorViewModel(null);
			vm.Password       = new ProtectedString(true, "abc");
			vm.PasswordRepeat = new ProtectedString(true, "abc");

			Assert.False(vm.PasswordMismatch);
		}

		[Fact]
		public void SaveCommand_CannotExecute_WhenTitleEmpty()
		{
			var vm = new EntryEditorViewModel(null);
			vm.Title = string.Empty;

			Assert.False(vm.SaveCommand.CanExecute(null));
		}

		[Fact]
		public void SaveCommand_CannotExecute_WhenPasswordMismatch()
		{
			var vm = new EntryEditorViewModel(null);
			vm.Title          = "Test";
			vm.Password       = new ProtectedString(true, "a");
			vm.PasswordRepeat = new ProtectedString(true, "b");

			Assert.False(vm.SaveCommand.CanExecute(null));
		}

		// ------------------------------------------------------------------ //
		// Save lifecycle                                                        //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Save_CreateMode_RaisesSaved()
		{
			var vm = new EntryEditorViewModel(null);
			vm.Title = "New Entry";
			vm.Password = vm.PasswordRepeat = new ProtectedString(true, "pass");

			bool saved = false;
			vm.Saved += (_, _) => saved = true;

			vm.SaveCommand.Execute(null);

			Assert.True(saved);
		}

		[Fact]
		public void Save_EditMode_CreatesHistoryBackupAndAppliesChanges()
		{
			var source = MakeFullEntry();
			uint historyBefore = source.History.UCount;

			var vm = new EntryEditorViewModel(source);
			vm.Title = "Gmail (updated)";
			vm.SaveCommand.Execute(null);

			Assert.Equal("Gmail (updated)", source.Strings.ReadSafe(PwDefs.TitleField));
			Assert.Equal(historyBefore + 1, source.History.UCount);
		}

		// ------------------------------------------------------------------ //
		// Cancel lifecycle                                                      //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Cancel_RaisesCancelled_DoesNotModifySource()
		{
			var source = MakeFullEntry();
			var vm = new EntryEditorViewModel(source);
			vm.Title = "Changed Title";

			bool cancelled = false;
			vm.Cancelled += (_, _) => cancelled = true;

			vm.CancelCommand.Execute(null);

			Assert.True(cancelled);
			Assert.Equal("Gmail", source.Strings.ReadSafe(PwDefs.TitleField));
		}

		// ------------------------------------------------------------------ //
		// FieldViewModel.ValueText                                             //
		// ------------------------------------------------------------------ //

		[Fact]
		public void FieldViewModel_ValueText_ProtectedField_ShowsMask()
		{
			var fvm = new FieldViewModel("TOTP", new ProtectedString(true, "123456"));
			Assert.Equal("••••••", fvm.ValueText);
		}

		[Fact]
		public void FieldViewModel_ValueText_PlainField_ShowsValue()
		{
			var fvm = new FieldViewModel("Website", new ProtectedString(false, "example.com"));
			Assert.Equal("example.com", fvm.ValueText);
		}

		// ------------------------------------------------------------------ //
		// No WinForms reference                                                //
		// ------------------------------------------------------------------ //

		[Fact]
		public void EntryEditorViewModel_Assembly_HasNoWinFormsReference()
		{
			var asm = typeof(EntryEditorViewModel).Assembly;
			bool hasWinForms = System.Linq.Enumerable.Any(
				asm.GetReferencedAssemblies(),
				r => r.Name != null &&
					r.Name.StartsWith("System.Windows.Forms", StringComparison.Ordinal));
			Assert.False(hasWinForms);
		}
	}
}
