using System;

using KeePass.Util;

namespace KeePass.Services
{
	/// <summary>
	/// Implements <see cref="IClipboardCredentialService"/> by wrapping
	/// <see cref="ClipboardUtil"/> with credential-aware countdown semantics.
	/// Encapsulates the <c>m_nClipClearMax</c> / <c>m_nClipClearCur</c> state
	/// that was previously inline in MainForm.
	/// </summary>
	public sealed class ClipboardCredentialService : IClipboardCredentialService
	{
		/// <summary>
		/// Maximum countdown value set when a credential is copied.
		/// 0 means auto-clear is disabled; negative means unconfigured.
		/// </summary>
		private int _clearMax;

		/// <summary>
		/// Current countdown tick.  -1 = idle, 0 = clear next tick,
		/// positive = counting down.
		/// </summary>
		private int _clearCur = -1;

		/// <param name="clearTimeoutSeconds">
		/// Global auto-clear timeout in seconds (from app configuration).
		/// Pass 0 to disable auto-clear.
		/// </param>
		public ClipboardCredentialService(int clearTimeoutSeconds)
		{
			_clearMax = clearTimeoutSeconds;
		}

		// ── IClipboardCredentialService ───────────────────────────────── //

		/// <inheritdoc/>
		public bool IsCountdownActive => _clearCur > 0;

		/// <inheritdoc/>
		public void StartCountdown(int clearTimeoutSeconds)
		{
			if(clearTimeoutSeconds < 0) { _clearCur = _clearMax; }
			else { _clearCur = clearTimeoutSeconds > 0 ? clearTimeoutSeconds : _clearMax; }
			_clearMax = clearTimeoutSeconds >= 0 ? clearTimeoutSeconds : _clearMax;
		}

		/// <inheritdoc/>
		public void Tick()
		{
			if(_clearCur > 0)
			{
				--_clearCur;
			}
			else if(_clearCur == 0)
			{
				_clearCur = -1;
				ClipboardUtil.ClearIfOwner();
			}
		}

		/// <inheritdoc/>
		public void ClearIfOwner()
		{
			_clearCur = -1;
			ClipboardUtil.ClearIfOwner();
		}

		/// <inheritdoc/>
		public string? GetCountdownStatusText()
		{
			if(_clearCur <= 0 || _clearMax <= 0) return null;
			return _clearCur.ToString() + " s";
		}

		/// <inheritdoc/>
		public double GetCountdownFraction()
		{
			if(_clearCur <= 0 || _clearMax <= 0) return 0.0;
			return Math.Clamp((double)_clearCur / _clearMax, 0.0, 1.0);
		}

		// ── Configuration update ──────────────────────────────────────── //

		/// <summary>
		/// Updates the auto-clear timeout from application configuration.
		/// Called when the user changes the setting in the security options dialog.
		/// </summary>
		public void SetClearTimeoutSeconds(int seconds)
		{
			_clearMax = seconds;
			// If a countdown is active and the new max is less than the current
			// counter, clamp immediately to avoid waiting past the new limit.
			if(_clearCur > _clearMax && _clearMax >= 0)
				_clearCur = _clearMax;
			else if(_clearMax < 0) _clearCur = 0;
		}
	}
}
