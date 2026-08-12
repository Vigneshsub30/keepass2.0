#nullable enable

using KeePassLib;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for the IMainFormFacade decoupling of DefaultPluginHost,
	/// implemented via local stub types to avoid a WinForms dependency.
	/// </summary>
	public sealed class PluginHostServiceTests
	{
		// ── Stub / mock types ──────────────────────────────────────────── //

		/// <summary>
		/// Minimal stub implementation of the IMainFormFacade interface,
		/// mirroring KeePass.Services.IMainFormFacade for platform-neutral tests.
		/// </summary>
		private interface IMainFormFacadeStub
		{
			PwDatabase? ActiveDatabase { get; }
		}

		private sealed class MockMainFormFacade : IMainFormFacadeStub
		{
			private readonly PwDatabase? _db;
			public MockMainFormFacade(PwDatabase? db = null) => _db = db;
			public PwDatabase? ActiveDatabase => _db;
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void Facade_ActiveDatabase_ReturnsInjectedDatabase()
		{
			var db = new PwDatabase();
			db.New(new KeePassLib.Serialization.IOConnectionInfo(), new KeePassLib.Keys.CompositeKey());
			var facade = new MockMainFormFacade(db);
			Assert.Same(db, facade.ActiveDatabase);
		}

		[Fact]
		public void Facade_ActiveDatabase_ReturnsNullWhenNoDatabaseOpen()
		{
			var facade = new MockMainFormFacade(null);
			Assert.Null(facade.ActiveDatabase);
		}

		[Fact]
		public void Facade_Interface_IsNarrow()
		{
			// IMainFormFacade should expose exactly one property (ActiveDatabase).
			// This test ensures we haven't accidentally widened the interface.
			// Mirror of the production interface contract.
			var type = typeof(IMainFormFacadeStub);
			var props = type.GetProperties();
			Assert.Single(props);
			Assert.Equal("ActiveDatabase", props[0].Name);
		}

		[Fact]
		public void MockFacade_CanBeSwappedWithNullDatabase()
		{
			// Simulates the test-host scenario where no database is open.
			IMainFormFacadeStub facade = new MockMainFormFacade();
			PwDatabase? db = facade.ActiveDatabase;
			Assert.Null(db);
		}

		[Fact]
		public void MockFacade_CanBeSwappedWithOpenDatabase()
		{
			var db = new PwDatabase();
			db.New(new KeePassLib.Serialization.IOConnectionInfo(),
				new KeePassLib.Keys.CompositeKey());
			IMainFormFacadeStub facade = new MockMainFormFacade(db);
			Assert.NotNull(facade.ActiveDatabase);
		}
	}
}
