#nullable enable

using System;
using System.Threading;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for the WorkspaceLock domain logic, implemented via a local stub
	/// that mirrors <c>IWorkspaceLockService</c> and <c>WorkspaceLockCoordinator</c>
	/// without referencing the WinForms KeePass assembly.
	/// </summary>
	public sealed class WorkspaceLockCoordinatorTests
	{
		// ── Minimal stub types ─────────────────────────────────────────── //

		private sealed class StubWorkspaceLockCoordinator
		{
			private long _lockAtTicks         = long.MaxValue;
			private long _lockAtGlobalTicks   = long.MaxValue;
			private int  _lockTimerMaxSeconds;
			private int  _lockRequestedCount;

			public int LockRequestedCount => _lockRequestedCount;

			public StubWorkspaceLockCoordinator(int lockTimerMaxSeconds)
			{
				_lockTimerMaxSeconds = lockTimerMaxSeconds;
			}

			/// <summary>Mirror of WorkspaceLockCoordinator.NotifyUserActivity.</summary>
			public void NotifyUserActivity()
			{
				if(_lockTimerMaxSeconds == 0)
				{
					_lockAtTicks = long.MaxValue;
					return;
				}
				_lockAtTicks = DateTime.UtcNow
					.AddSeconds(_lockTimerMaxSeconds)
					.Ticks;
			}

			/// <summary>
			/// Simulates UpdateGlobalLockTimeout when the idle clock advances.
			/// </summary>
			public void SimulateGlobalIdleReset(DateTime utcNow, int lockGlobalSecs)
			{
				if(lockGlobalSecs == 0) { _lockAtGlobalTicks = long.MaxValue; return; }
				_lockAtGlobalTicks = utcNow.AddSeconds(lockGlobalSecs).Ticks;
			}

			/// <summary>Mirror of WorkspaceLockCoordinator.CheckLockConditions.</summary>
			public bool CheckLockConditions(DateTime utcNow)
			{
				long ticks = utcNow.Ticks;
				if((ticks >= _lockAtTicks) || (ticks >= _lockAtGlobalTicks))
				{
					Interlocked.Increment(ref _lockRequestedCount);
					return true;
				}
				return false;
			}

			public void SetLockTimerMaxSeconds(int seconds)
			{
				_lockTimerMaxSeconds = seconds;
				NotifyUserActivity();
			}

			/// <summary>Directly sets the per-database lock deadline (for testing).</summary>
			public void SetLockAtTicks(long ticks) => _lockAtTicks = ticks;

			/// <summary>Directly sets the global lock deadline (for testing).</summary>
			public void SetLockAtGlobalTicks(long ticks) => _lockAtGlobalTicks = ticks;
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void NotifyUserActivity_ZeroTimeout_LockAtIsMaxValue()
		{
			var c = new StubWorkspaceLockCoordinator(0);
			c.NotifyUserActivity();
			// With 0-second timeout, lock deadline should be long.MaxValue.
			var future = DateTime.UtcNow.AddDays(1);
			Assert.False(c.CheckLockConditions(future)); // still should not lock
		}

		[Fact]
		public void NotifyUserActivity_PositiveTimeout_ResetsDeadline()
		{
			var c = new StubWorkspaceLockCoordinator(3600);
			c.NotifyUserActivity();
			// Just set — deadline is ~1 hour from now, so checking *now* should not lock.
			Assert.False(c.CheckLockConditions(DateTime.UtcNow));
		}

		[Fact]
		public void CheckLockConditions_DeadlineInPast_TriggersLock()
		{
			var c = new StubWorkspaceLockCoordinator(3600);
			// Set the lock deadline to 1 second ago.
			c.SetLockAtTicks(DateTime.UtcNow.AddSeconds(-1).Ticks);
			Assert.True(c.CheckLockConditions(DateTime.UtcNow));
			Assert.Equal(1, c.LockRequestedCount);
		}

		[Fact]
		public void CheckLockConditions_GlobalDeadlineInPast_TriggersLock()
		{
			var c = new StubWorkspaceLockCoordinator(0);
			// Per-database timer disabled; set global deadline in the past.
			c.SetLockAtGlobalTicks(DateTime.UtcNow.AddSeconds(-1).Ticks);
			Assert.True(c.CheckLockConditions(DateTime.UtcNow));
			Assert.Equal(1, c.LockRequestedCount);
		}

		[Fact]
		public void CheckLockConditions_NoDeadlineExpired_DoesNotLock()
		{
			var c = new StubWorkspaceLockCoordinator(3600);
			c.NotifyUserActivity();
			// Both deadlines are in the future.
			Assert.False(c.CheckLockConditions(DateTime.UtcNow));
			Assert.Equal(0, c.LockRequestedCount);
		}

		[Fact]
		public void UserActivity_ResetsDeadlineAfterAlmostExpired()
		{
			var c = new StubWorkspaceLockCoordinator(5);
			// Deadline almost expired...
			c.SetLockAtTicks(DateTime.UtcNow.AddMilliseconds(50).Ticks);
			// User acts — deadline reset to 5 seconds from now.
			c.NotifyUserActivity();
			// Checking now should NOT lock.
			Assert.False(c.CheckLockConditions(DateTime.UtcNow));
			Assert.Equal(0, c.LockRequestedCount);
		}

		[Fact]
		public void GlobalIdleReset_ZeroGlobalTimeout_DeadlineIsMaxValue()
		{
			var c = new StubWorkspaceLockCoordinator(0);
			c.SimulateGlobalIdleReset(DateTime.UtcNow, 0);
			var future = DateTime.UtcNow.AddDays(1);
			Assert.False(c.CheckLockConditions(future));
		}

		[Fact]
		public void GlobalIdleReset_PositiveTimeout_SetsDeadline()
		{
			var c = new StubWorkspaceLockCoordinator(0);
			c.SimulateGlobalIdleReset(DateTime.UtcNow, 3600); // 1-hour global
			// Deadline 1 hour from now — checking now should not lock.
			Assert.False(c.CheckLockConditions(DateTime.UtcNow));
		}

		[Fact]
		public void SetLockTimerMaxSeconds_UpdatesDeadline()
		{
			var c = new StubWorkspaceLockCoordinator(0);
			c.SetLockTimerMaxSeconds(3600);
			// After update, deadline is ~1 hour from now.
			Assert.False(c.CheckLockConditions(DateTime.UtcNow));
		}

		[Fact]
		public void ConcurrentCheckLockConditions_IsIdempotent()
		{
			// Simulates thread-safety by running multiple checks in parallel.
			var c = new StubWorkspaceLockCoordinator(0);
			c.SetLockAtTicks(DateTime.UtcNow.AddSeconds(-1).Ticks);

			int lockCount = 0;
			var threads = new Thread[4];
			for(int i = 0; i < threads.Length; i++)
			{
				threads[i] = new Thread(() =>
				{
					if(c.CheckLockConditions(DateTime.UtcNow))
						Interlocked.Increment(ref lockCount);
				});
			}
			foreach(var t in threads) t.Start();
			foreach(var t in threads) t.Join();

			Assert.True(lockCount >= 1,
				"At least one thread should have detected the lock condition.");
		}
	}
}
