#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for the <see cref="PluginLoadContext"/> behavior using its
	/// loading logic via a stub that mimics the production class.
	/// </summary>
	/// <remarks>
	/// We cannot reference <c>KeePass.Plugins.PluginLoadContext</c> directly
	/// (it lives in the WinForms-only KeePass project).  These tests instead
	/// verify the same design contract — host-assembly fallback and local
	/// resolution — with a local replica of the key logic.
	/// </remarks>
	public sealed class PluginLoadContextTests
	{
		// ── Local replica of the key contract ────────────────────────── //

		/// <summary>
		/// Minimal replica of <c>PluginLoadContext</c> for testing.
		/// </summary>
		private sealed class TestPluginLoadContext : AssemblyLoadContext
		{
			private readonly string _dir;
			private readonly System.Collections.Generic.HashSet<string> _hostNames;

			public TestPluginLoadContext(string pluginFilePath)
				: base(name: Path.GetFileNameWithoutExtension(pluginFilePath),
					   isCollectible: true)
			{
				_dir = Path.GetDirectoryName(Path.GetFullPath(pluginFilePath))!;
				_hostNames = new System.Collections.Generic.HashSet<string>(
					StringComparer.OrdinalIgnoreCase)
				{
					"KeePass",
					"KeePassLib",
					"KeePass.Core",
					"KeePass.Platform.Unix",
				};
			}

			protected override Assembly? Load(AssemblyName assemblyName)
			{
				string name = assemblyName.Name ?? string.Empty;
				if (_hostNames.Contains(name)) return null; // host fallback

				string local = Path.Combine(_dir, name + ".dll");
				if (File.Exists(local)) return LoadFromAssemblyPath(local);

				return null;
			}

			public bool IsHostName(string name) => _hostNames.Contains(name);
		}

		// ── Tests ─────────────────────────────────────────────────────── //

		[Fact]
		public void Context_IsCollectible()
		{
			string selfPath = typeof(PluginLoadContextTests).Assembly.Location;
			var ctx = new TestPluginLoadContext(selfPath);
			Assert.True(ctx.IsCollectible);
			ctx.Unload();
		}

		[Fact]
		public void Context_Name_IsAssemblyBaseName()
		{
			string selfPath = typeof(PluginLoadContextTests).Assembly.Location;
			string expected = Path.GetFileNameWithoutExtension(selfPath);
			var ctx = new TestPluginLoadContext(selfPath);
			Assert.Equal(expected, ctx.Name);
			ctx.Unload();
		}

		[Fact]
		public void HostAssemblies_KeePass_IsHostName()
		{
			string selfPath = typeof(PluginLoadContextTests).Assembly.Location;
			var ctx = new TestPluginLoadContext(selfPath);
			Assert.True(ctx.IsHostName("KeePass"));
			Assert.True(ctx.IsHostName("KeePassLib"));
			Assert.True(ctx.IsHostName("KeePass.Core"));
			ctx.Unload();
		}

		[Fact]
		public void HostAssemblies_ExternalLib_IsNotHostName()
		{
			string selfPath = typeof(PluginLoadContextTests).Assembly.Location;
			var ctx = new TestPluginLoadContext(selfPath);
			Assert.False(ctx.IsHostName("SomePluginDependency"));
			ctx.Unload();
		}

		[Fact]
		public void HostAssemblies_CaseInsensitive()
		{
			string selfPath = typeof(PluginLoadContextTests).Assembly.Location;
			var ctx = new TestPluginLoadContext(selfPath);
			Assert.True(ctx.IsHostName("KEEPASS"));
			Assert.True(ctx.IsHostName("keepasslib"));
			ctx.Unload();
		}

		[Fact]
		public void Context_Unload_CanBeCalledTwice_NoException()
		{
			string selfPath = typeof(PluginLoadContextTests).Assembly.Location;
			var ctx = new TestPluginLoadContext(selfPath);
			ctx.Unload();
			// Second unload should not throw (ALC Unload is idempotent).
			var ex = Record.Exception(() => ctx.Unload());
			Assert.Null(ex);
		}

		[Fact]
		public void TwoContexts_SeparateNames_BothCollectible()
		{
			string selfPath = typeof(PluginLoadContextTests).Assembly.Location;
			var ctx1 = new TestPluginLoadContext(selfPath);
			var ctx2 = new TestPluginLoadContext(selfPath);

			Assert.True(ctx1.IsCollectible);
			Assert.True(ctx2.IsCollectible);

			ctx1.Unload();
			ctx2.Unload();
		}
	}
}
