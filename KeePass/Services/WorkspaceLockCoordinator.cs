using System;

using KeePass.App.Configuration;
using KeePass.Native;
using KeePass.Util;

using Microsoft.Extensions.Logging;

namespace KeePass.Services
{
	/// <summary>
	/// Implements <see cref="IWorkspaceLockService"/> by encapsulating all
	/// workspace-lock timer state that was previously scattered across
	/// MainForm and MainForm_Functions.
	///
	/// Thread safety: <see cref="CheckLockConditions"/> uses a
	/// <see cref="CriticalSectionEx"/> to guard timer reads, matching the
	/// semantics of the original inline code in OnTimerMainTick.
	/// </summary>
	public sealed class WorkspaceLockCoordinator : IWorkspaceLockService, IDisposable
	{
		private static readonly ILogger<WorkspaceLockCoordinator> s_log =
			Program.LoggerFactory.CreateLogger<WorkspaceLockCoordinator>();

		private readonly CriticalSectionEx _csLockTimer = new CriticalSectionEx();

		/// <summary>Ticks at which the per-database inactivity deadline expires.</summary>
		private long _lockAtTicks = long.MaxValue;

		/// <summary>Ticks at which the global idle deadline expires.</summary>
		private long _lockAtGlobalTicks = long.MaxValue;

		/// <summary>Last known raw input-idle timestamp from the OS.</summary>
		private uint _lastInputTime = uint.MaxValue;

		/// <summary>Inactivity timeout in seconds (0 = disabled).</summary>
		private int _lockTimerMaxSeconds;

		private readonly SessionLockNotifier _sessionLockNotifier = new SessionLockNotifier();
		private bool _disposed;

		/// <inheritdoc/>
		public event Action? OnLockRequested;

		/// <param name="lockTimerMaxSeconds">
		/// Initial per-database inactivity timeout in seconds.
		/// Pass 0 to disable the per-database timer.
		/// </param>
		public WorkspaceLockCoordinator(int lockTimerMaxSeconds)
		{
			_lockTimerMaxSeconds = lockTimerMaxSeconds;

			_sessionLockNotifier.Install(OnSessionLock);
		}

		// ── IWorkspaceLockService ─────────────────────────────────────── //

		/// <inheritdoc/>
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

		/// <inheritdoc/>
		public void UpdateGlobalLockTimeout(DateTime utcNow)
		{
			uint uLockGlobal = Program.Config.Security.WorkspaceLocking.LockAfterGlobalTime;
			if(uLockGlobal == 0)
			{
				_lockAtGlobalTicks = long.MaxValue;
				return;
			}

			uint? uLastInputTime = NativeMethods.GetLastInputTime();
			if(!uLastInputTime.HasValue) return;

			if(uLastInputTime.Value != _lastInputTime)
			{
				_lockAtGlobalTicks = utcNow.AddSeconds(uLockGlobal).Ticks;
				_lastInputTime = uLastInputTime.Value;
			}
		}

		/// <inheritdoc/>
		public bool CheckLockConditions(DateTime utcNow)
		{
			if(!_csLockTimer.TryEnter()) return false;
			try
			{
				long lCurTicks = utcNow.Ticks;
				bool bInactivity = lCurTicks >= _lockAtTicks;
				bool bGlobalIdle  = lCurTicks >= _lockAtGlobalTicks;

				if(bInactivity || bGlobalIdle)
				{
					string trigger = bInactivity ? "Inactivity" : "GlobalIdle";
					s_log.LogInformation(
						"Workspace locked. Trigger: {Trigger}, UtcNow: {UtcNow}",
						trigger,
						utcNow.ToString("o"));
					OnLockRequested?.Invoke();
					return true;
				}
				return false;
			}
			finally { _csLockTimer.Exit(); }
		}

		// ── Public convenience ────────────────────────────────────────── //

		/// <summary>
		/// Updates the per-database inactivity timeout (e.g. when the user
		/// changes the setting in the security options dialog).
		/// </summary>
		public void SetLockTimerMaxSeconds(int seconds)
		{
			_lockTimerMaxSeconds = seconds;
			NotifyUserActivity(); // Recalculate deadline from now.
		}

		// ── SessionLockNotifier callback ──────────────────────────────── //

		private void OnSessionLock(object? sender, SessionLockEventArgs e)
		{
			App.Configuration.AceWorkspaceLocking wl =
				Program.Config.Security.WorkspaceLocking;

			bool bLock =
				(e.Reason == SessionLockReason.Lock         && wl.LockOnSessionSwitch) ||
				(e.Reason == SessionLockReason.UserSwitch   && wl.LockOnSessionSwitch) ||
				(e.Reason == SessionLockReason.Suspend      && wl.LockOnSuspend);

			if(bLock)
			{
				s_log.LogInformation(
					"Workspace locked. Trigger: {Trigger}, SessionReason: {Reason}, " +
					"UtcNow: {UtcNow}",
					"SessionEvent",
					e.Reason.ToString(),
					DateTime.UtcNow.ToString("o"));
				OnLockRequested?.Invoke();
			}
		}

		// ── IDisposable ───────────────────────────────────────────────── //

		public void Dispose()
		{
			if(_disposed) return;
			_disposed = true;
			_sessionLockNotifier.Uninstall();
		}
	}
}
