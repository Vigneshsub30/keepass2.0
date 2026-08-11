#nullable enable

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for the platform-workaround service pattern.
	/// Uses a local stub to keep this test assembly platform-neutral
	/// (no reference to the WinForms KeePass assembly).
	/// </summary>
	public sealed class PlatformWorkaroundServiceTests
	{
		// ── Stub types ─────────────────────────────────────────────────── //

		private sealed class StubConfig
		{
			public bool PreventScreenCapture      { get; set; } = true;
			public bool MasterKeyOnSecureDesktop  { get; set; } = true;
			public long HotKeyGlobalAutoType      { get; set; } = 1L;
			public long HotKeyGlobalAutoTypePassword { get; set; } = 2L;
			public long HotKeySelectedAutoType    { get; set; } = 3L;
			public long HotKeyShowWindow          { get; set; } = 4L;
			public long HotKeyEntryMenu           { get; set; } = 5L;
		}

		private interface IStubPlatformWorkaroundService
		{
			void ApplyConfigWorkarounds(StubConfig config, bool isUnix);
		}

		private sealed class StubPlatformWorkaroundService : IStubPlatformWorkaroundService
		{
			public void ApplyConfigWorkarounds(StubConfig config, bool isUnix)
			{
				if(config == null) return;
				if(!isUnix) return;

				config.PreventScreenCapture      = false;
				config.MasterKeyOnSecureDesktop  = false;
				config.HotKeyGlobalAutoType      = 0L;
				config.HotKeyGlobalAutoTypePassword = 0L;
				config.HotKeySelectedAutoType    = 0L;
				config.HotKeyShowWindow          = 0L;
				config.HotKeyEntryMenu           = 0L;
			}
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void ApplyConfigWorkarounds_NonUnix_LeavesConfigUnchanged()
		{
			var svc    = new StubPlatformWorkaroundService();
			var config = new StubConfig();
			svc.ApplyConfigWorkarounds(config, isUnix: false);

			Assert.True(config.PreventScreenCapture);
			Assert.True(config.MasterKeyOnSecureDesktop);
			Assert.Equal(1L, config.HotKeyGlobalAutoType);
			Assert.Equal(2L, config.HotKeyGlobalAutoTypePassword);
			Assert.Equal(3L, config.HotKeySelectedAutoType);
			Assert.Equal(4L, config.HotKeyShowWindow);
			Assert.Equal(5L, config.HotKeyEntryMenu);
		}

		[Fact]
		public void ApplyConfigWorkarounds_Unix_DisablesWindowsOnlyOptions()
		{
			var svc    = new StubPlatformWorkaroundService();
			var config = new StubConfig();
			svc.ApplyConfigWorkarounds(config, isUnix: true);

			Assert.False(config.PreventScreenCapture);
			Assert.False(config.MasterKeyOnSecureDesktop);
			Assert.Equal(0L, config.HotKeyGlobalAutoType);
			Assert.Equal(0L, config.HotKeyGlobalAutoTypePassword);
			Assert.Equal(0L, config.HotKeySelectedAutoType);
			Assert.Equal(0L, config.HotKeyShowWindow);
			Assert.Equal(0L, config.HotKeyEntryMenu);
		}

		[Fact]
		public void ApplyConfigWorkarounds_NullConfig_DoesNotThrow()
		{
			var svc = new StubPlatformWorkaroundService();
			// Must not throw.
			svc.ApplyConfigWorkarounds(null!, isUnix: true);
		}

		[Fact]
		public void ApplyConfigWorkarounds_Unix_AllHotKeysSetToNone()
		{
			var svc    = new StubPlatformWorkaroundService();
			var config = new StubConfig();
			svc.ApplyConfigWorkarounds(config, isUnix: true);

			long none = 0L; // Keys.None == 0
			Assert.Equal(none, config.HotKeyGlobalAutoType);
			Assert.Equal(none, config.HotKeyGlobalAutoTypePassword);
			Assert.Equal(none, config.HotKeySelectedAutoType);
			Assert.Equal(none, config.HotKeyShowWindow);
			Assert.Equal(none, config.HotKeyEntryMenu);
		}

		[Fact]
		public void ApplyConfigWorkarounds_CalledTwice_IsIdempotent()
		{
			var svc    = new StubPlatformWorkaroundService();
			var config = new StubConfig();
			svc.ApplyConfigWorkarounds(config, isUnix: true);
			svc.ApplyConfigWorkarounds(config, isUnix: true);

			// Second call should produce the same result.
			Assert.False(config.PreventScreenCapture);
			Assert.Equal(0L, config.HotKeyGlobalAutoType);
		}
	}
}
