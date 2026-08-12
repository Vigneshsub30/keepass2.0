using KeePass.Core.Services;
using KeePass.Core.ViewModels;
using KeePass.Desktop.Avalonia.Views;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	/// <summary>
	/// Unit tests for <see cref="ViewLocator"/> — exercises the naming-convention
	/// resolution without needing a live Avalonia application.
	/// </summary>
	public sealed class ViewLocatorTests
	{
		// ------------------------------------------------------------------ //
		// ResolveViewTypeName                                                  //
		// ------------------------------------------------------------------ //

		[Fact]
		public void ResolveViewTypeName_MainWindowViewModel_MapsToMainWindowView()
		{
			string viewTypeName = ViewLocator.ResolveViewTypeName(typeof(MainWindowViewModel));

			Assert.Contains("MainWindowView", viewTypeName);
		}

		[Fact]
		public void ResolveViewTypeName_NonViewModelType_ReturnsEmpty()
		{
			// A type whose name does not end in "ViewModel" should return empty.
			string result = ViewLocator.ResolveViewTypeName(typeof(string));

			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void ResolveViewTypeName_NullType_ThrowsArgumentNullException()
		{
			Assert.Throws<System.ArgumentNullException>(() =>
				ViewLocator.ResolveViewTypeName(null!));
		}

		// ------------------------------------------------------------------ //
		// Match                                                                //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Match_ObservableObject_ReturnsTrue()
		{
			var locator = new ViewLocator();
			var sp = TestServiceProvider.Build();
			var vm = (MainWindowViewModel)sp.GetService(typeof(MainWindowViewModel))!;

			Assert.True(locator.Match(vm));
		}

		[Fact]
		public void Match_PlainObject_ReturnsFalse()
		{
			var locator = new ViewLocator();

			Assert.False(locator.Match("plain string"));
			Assert.False(locator.Match(42));
			Assert.False(locator.Match(null));
		}

		// ------------------------------------------------------------------ //
		// Build                                                                //
		// ------------------------------------------------------------------ //

		[Fact]
		public void Build_Null_ReturnsNull()
		{
			var locator = new ViewLocator();

			var result = locator.Build(null);

			Assert.Null(result);
		}

		// ------------------------------------------------------------------ //
		// DI container registration                                            //
		// ------------------------------------------------------------------ //

		[Fact]
		public void BuildServiceProvider_ResolvesMainWindowViewModel()
		{
			var sp = App.BuildServiceProvider();
			var vm = sp.GetService(typeof(MainWindowViewModel));

			Assert.NotNull(vm);
			Assert.IsType<MainWindowViewModel>(vm);
		}

		[Fact]
		public void TestServiceProvider_ResolvesMainWindowViewModel()
		{
			var sp = TestServiceProvider.Build();
			var vm = sp.GetService(typeof(MainWindowViewModel));

			Assert.NotNull(vm);
			Assert.IsType<MainWindowViewModel>(vm);
		}

		// ------------------------------------------------------------------ //
		// MainWindowViewModel instantiation                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void MainWindowViewModel_InitialState_NoDatabasesOpen()
		{
			var sp = TestServiceProvider.Build();
			var vm = (MainWindowViewModel)sp.GetService(typeof(MainWindowViewModel))!;

			Assert.Empty(vm.Databases);
			Assert.Equal(-1, vm.ActiveDatabaseIndex);
		}

		[Fact]
		public void MainWindowViewModel_AfterAddDatabase_DatabasesUpdated()
		{
			var sp = TestServiceProvider.Build();
			var sessionService = (TestServiceProvider.StubDatabaseSessionService)
				sp.GetService(typeof(IDatabaseSessionService))!;
			var vm = (MainWindowViewModel)sp.GetService(typeof(MainWindowViewModel))!;

			// Refresh state by registering a SessionChanged handler that the
			// StubDatabaseSessionService fires when AddDatabase is called.
			int refreshCount = 0;
			vm.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(MainWindowViewModel.Databases))
					refreshCount++;
			};

			var db = TestDatabaseFactory.CreateSample("TestDB.kdbx");
			sessionService.AddDatabase(db);

			Assert.Single(vm.Databases);
			Assert.Equal("TestDB", vm.Databases[0].Name);
		}
	}
}
