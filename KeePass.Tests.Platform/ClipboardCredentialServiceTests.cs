#nullable enable

using System;
using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for clipboard credential auto-clear countdown logic, implemented
	/// via a local stub that mirrors <c>ClipboardCredentialService</c> without
	/// referencing the WinForms KeePass assembly.
	/// </summary>
	public sealed class ClipboardCredentialServiceTests
	{
		// ── Minimal stub ───────────────────────────────────────────────── //

		private sealed class StubClipboardCredentialService
		{
			private int _clearMax;
			private int _clearCur = -1;
			public int ClearCallCount { get; private set; }

			public StubClipboardCredentialService(int clearTimeoutSeconds)
			{
				_clearMax = clearTimeoutSeconds;
			}

			public bool IsCountdownActive => _clearCur > 0;

			public void StartCountdown(int clearTimeoutSeconds)
			{
				if(clearTimeoutSeconds < 0)
					_clearCur = _clearMax;
				else
				{
					_clearMax = clearTimeoutSeconds;
					_clearCur = _clearMax;
				}
			}

			public void Tick()
			{
				if(_clearCur > 0) { --_clearCur; }
				else if(_clearCur == 0) { _clearCur = -1; ++ClearCallCount; }
			}

			public void ClearIfOwner()
			{
				_clearCur = -1;
				++ClearCallCount;
			}

			public string? GetCountdownStatusText()
			{
				if(_clearCur <= 0 || _clearMax <= 0) return null;
				return _clearCur.ToString() + " s";
			}

			public double GetCountdownFraction()
			{
				if(_clearCur <= 0 || _clearMax <= 0) return 0.0;
				return Math.Clamp((double)_clearCur / _clearMax, 0.0, 1.0);
			}

			public void SetClearTimeoutSeconds(int seconds)
			{
				_clearMax = seconds;
				if(_clearCur > _clearMax && _clearMax >= 0) _clearCur = _clearMax;
				else if(_clearMax < 0) _clearCur = 0;
			}
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void StartCountdown_SetsCountdown()
		{
			var s = new StubClipboardCredentialService(0);
			s.StartCountdown(10);
			Assert.True(s.IsCountdownActive);
		}

		[Fact]
		public void Tick_DecrementsCountdown()
		{
			var s = new StubClipboardCredentialService(5);
			s.StartCountdown(5);
			s.Tick(); // 4
			s.Tick(); // 3
			Assert.True(s.IsCountdownActive);
			Assert.Equal(0, s.ClearCallCount);
		}

		[Fact]
		public void Tick_CountdownReachesZero_ClearsClipboard()
		{
			var s = new StubClipboardCredentialService(2);
			s.StartCountdown(2);
			s.Tick(); // 1
			s.Tick(); // 0
			s.Tick(); // triggers clear
			Assert.False(s.IsCountdownActive);
			Assert.Equal(1, s.ClearCallCount);
		}

		[Fact]
		public void Tick_WhenIdle_DoesNotClear()
		{
			var s = new StubClipboardCredentialService(10);
			// No StartCountdown call — countdown is idle.
			s.Tick();
			s.Tick();
			Assert.Equal(0, s.ClearCallCount);
		}

		[Fact]
		public void ClearIfOwner_ResetsCountdown()
		{
			var s = new StubClipboardCredentialService(10);
			s.StartCountdown(10);
			s.ClearIfOwner();
			Assert.False(s.IsCountdownActive);
			Assert.Equal(1, s.ClearCallCount);
		}

		[Fact]
		public void MultipleRapidCopies_ResetsCountdown()
		{
			var s = new StubClipboardCredentialService(5);
			s.StartCountdown(5);
			s.Tick(); s.Tick(); // Countdown at 3
			// User copies again — countdown resets to 5.
			s.StartCountdown(5);
			s.Tick(); s.Tick(); s.Tick(); s.Tick(); s.Tick(); // 0
			s.Tick(); // triggers clear
			Assert.Equal(1, s.ClearCallCount);
		}

		[Fact]
		public void GetCountdownStatusText_ReturnsNullWhenIdle()
		{
			var s = new StubClipboardCredentialService(10);
			Assert.Null(s.GetCountdownStatusText());
		}

		[Fact]
		public void GetCountdownStatusText_ReturnsTextWhenActive()
		{
			var s = new StubClipboardCredentialService(10);
			s.StartCountdown(10);
			string? text = s.GetCountdownStatusText();
			Assert.NotNull(text);
			Assert.Contains("10", text!);
		}

		[Fact]
		public void GetCountdownFraction_ReturnsZeroWhenIdle()
		{
			var s = new StubClipboardCredentialService(10);
			Assert.Equal(0.0, s.GetCountdownFraction());
		}

		[Fact]
		public void GetCountdownFraction_ReturnsOneAtStart()
		{
			var s = new StubClipboardCredentialService(10);
			s.StartCountdown(10);
			Assert.Equal(1.0, s.GetCountdownFraction(), precision: 5);
		}

		[Fact]
		public void SetClearTimeoutSeconds_ClampsRunningCountdown()
		{
			var s = new StubClipboardCredentialService(10);
			s.StartCountdown(10); // at 10
			s.SetClearTimeoutSeconds(3); // clamp to 3
			// Countdown should now be at or below 3.
			Assert.True(s.IsCountdownActive);
		}
	}
}
