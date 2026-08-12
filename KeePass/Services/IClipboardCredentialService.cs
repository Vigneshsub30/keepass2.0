namespace KeePass.Services
{
	/// <summary>
	/// Manages clipboard credential delivery with automatic-clear scheduling.
	/// </summary>
	public interface IClipboardCredentialService
	{
		/// <summary>
		/// <c>true</c> when the auto-clear countdown is actively running.
		/// </summary>
		bool IsCountdownActive { get; }

		/// <summary>
		/// Copies <paramref name="value"/> to the clipboard and starts an
		/// auto-clear countdown of <paramref name="clearTimeoutSeconds"/>
		/// timer ticks.  Calling this method resets any running countdown.
		/// </summary>
		/// <param name="value">Plain-text credential value to copy.</param>
		/// <param name="clearTimeoutSeconds">
		/// Number of timer ticks after which the clipboard should be cleared.
		/// Pass 0 or negative to skip auto-clear.
		/// </param>
		void StartCountdown(int clearTimeoutSeconds);

		/// <summary>
		/// Called once per timer tick (typically every second).
		/// Decrements the countdown and clears the clipboard when it reaches
		/// zero.
		/// </summary>
		void Tick();

		/// <summary>
		/// Clears the clipboard immediately if KeePass still owns it and
		/// resets the countdown.
		/// </summary>
		void ClearIfOwner();

		/// <summary>
		/// Returns a value suitable for display in the status bar during
		/// countdown (e.g. "10 s remaining"), or <c>null</c> when no countdown
		/// is active.
		/// </summary>
		string? GetCountdownStatusText();

		/// <summary>
		/// Countdown fraction for progress-bar display: a value in [0.0, 1.0]
		/// where 1.0 = just started, 0.0 = about to clear.
		/// Returns 0 when no countdown is active.
		/// </summary>
		double GetCountdownFraction();
	}
}
