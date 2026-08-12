#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KeePassLib.Plugins;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="IApplicationHost"/> and
	/// <see cref="LegacyPluginHostAdapter"/> using only platform-neutral types
	/// so no WinForms or Avalonia assembly is required at test time.
	/// </summary>
	public sealed class PluginHostV2Tests
	{
		// ------------------------------------------------------------------ //
		// Stub                                                                //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// Minimal <see cref="IApplicationHost"/> that records calls and has
		/// no dependency on WinForms or Avalonia.
		/// </summary>
		private sealed class StubApplicationHost : IApplicationHost
		{
			public bool   IsMainWindowVisible { get; set; } = true;
			public string PlatformName        => "Stub";

			public readonly List<string> StatusMessages  = new();
			public readonly List<Action> UIThreadActions = new();
			public          int          BringToForegroundCallCount;
			public          int          RefreshEntryListCallCount;
			public          bool         SaveAllDatabasesResult = true;

			public void BringToForeground()     => BringToForegroundCallCount++;
			public void RefreshEntryList()      => RefreshEntryListCallCount++;
			public bool SaveAllDatabases()      => SaveAllDatabasesResult;

			public void ShowStatusMessage(string message) => StatusMessages.Add(message);

			public void InvokeOnUIThread(Action action)
			{
				UIThreadActions.Add(action);
				action();
			}

			public Task InvokeOnUIThreadAsync(Action action)
			{
				UIThreadActions.Add(action);
				action();
				return Task.CompletedTask;
			}
		}

		// ------------------------------------------------------------------ //
		// IApplicationHost                                                    //
		// ------------------------------------------------------------------ //

		[Fact]
		public void StubHost_CanBeConstructed_WithoutWinForms()
		{
			var host = new StubApplicationHost();
			Assert.Equal("Stub", host.PlatformName);
		}

		[Fact]
		public void IsMainWindowVisible_ReflectsSetProperty()
		{
			var host = new StubApplicationHost { IsMainWindowVisible = false };
			Assert.False(host.IsMainWindowVisible);
		}

		[Fact]
		public void ShowStatusMessage_AddsToList()
		{
			var host = new StubApplicationHost();
			host.ShowStatusMessage("Hello");
			host.ShowStatusMessage("World");

			Assert.Equal(2, host.StatusMessages.Count);
			Assert.Equal("Hello", host.StatusMessages[0]);
			Assert.Equal("World", host.StatusMessages[1]);
		}

		[Fact]
		public void BringToForeground_IncrementsCounter()
		{
			var host = new StubApplicationHost();
			host.BringToForeground();
			host.BringToForeground();

			Assert.Equal(2, host.BringToForegroundCallCount);
		}

		[Fact]
		public void InvokeOnUIThread_ExecutesActionInline()
		{
			var host = new StubApplicationHost();
			bool ran = false;
			host.InvokeOnUIThread(() => ran = true);

			Assert.True(ran);
			Assert.Single(host.UIThreadActions);
		}

		[Fact]
		public async Task InvokeOnUIThreadAsync_CompletesTask()
		{
			var host = new StubApplicationHost();
			int value = 0;
			await host.InvokeOnUIThreadAsync(() => value = 42);

			Assert.Equal(42, value);
		}

		[Fact]
		public void RefreshEntryList_IncrementsCounter()
		{
			var host = new StubApplicationHost();
			host.RefreshEntryList();
			Assert.Equal(1, host.RefreshEntryListCallCount);
		}

		[Fact]
		public void SaveAllDatabases_ReturnsFalseWhenResultFalse()
		{
			var host = new StubApplicationHost { SaveAllDatabasesResult = false };
			Assert.False(host.SaveAllDatabases());
		}

		[Fact]
		public void SaveAllDatabases_ReturnsTrueWhenResultTrue()
		{
			var host = new StubApplicationHost { SaveAllDatabasesResult = true };
			Assert.True(host.SaveAllDatabases());
		}

		[Fact]
		public void PlatformName_DoesNotContainForm()
		{
			// Confirms the stub (and by convention any non-WinForms host)
			// does not expose "Form" in its platform name.
			var host = new StubApplicationHost();
			Assert.DoesNotContain("Form", host.PlatformName, StringComparison.OrdinalIgnoreCase);
		}
	}
}
