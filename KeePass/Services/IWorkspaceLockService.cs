using System;

namespace KeePass.Services
{
	/// <summary>
	/// Manages workspace-locking state: per-database inactivity timers,
	/// global idle detection, and session-lock signals.
	/// </summary>
	public interface IWorkspaceLockService
	{
		/// <summary>
		/// Raised when the service determines the workspace should be locked.
		/// MainForm subscribes and calls LockAllDocuments in response.
		/// </summary>
		event Action OnLockRequested;

		/// <summary>
		/// Informs the service that the user performed an activity, resetting
		/// the per-database inactivity timer.
		/// </summary>
		void NotifyUserActivity();

		/// <summary>
		/// Recalculates the global idle timeout using the current input timestamp
		/// obtained from the platform.
		/// </summary>
		/// <param name="utcNow">The current UTC time.</param>
		void UpdateGlobalLockTimeout(DateTime utcNow);

		/// <summary>
		/// Evaluates whether any lock condition is currently met.
		/// </summary>
		/// <param name="utcNow">The current UTC time.</param>
		/// <returns>
		/// <c>true</c> if a lock condition is met and
		/// <see cref="OnLockRequested"/> has been raised; <c>false</c> otherwise.
		/// </returns>
		bool CheckLockConditions(DateTime utcNow);
	}
}
