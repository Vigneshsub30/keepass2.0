/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System;
using System.Security.Cryptography;
using System.Threading;

namespace KeePass.Core.Platform
{
	/// <summary>
	/// Abstract base class for <see cref="IClipboardService"/> implementations.
	///
	/// <para>Encapsulates the two cross-platform concerns:</para>
	/// <list type="bullet">
	///   <item><description>
	///     <b>Ownership hash</b> — a SHA-256 digest of the last text written by
	///     this application, used by <see cref="ClearIfOwner"/> to avoid
	///     clearing content placed by another program (matches the
	///     <c>g_pbDataHash</c> pattern from <c>ClipboardUtil.cs</c>).
	///   </description></item>
	///   <item><description>
	///     <b>Auto-clear timer</b> — a <see cref="System.Threading.Timer"/>
	///     that calls <see cref="ClearIfOwner"/> after a configurable number of
	///     seconds.  Cancels automatically when the clipboard owner changes.
	///   </description></item>
	/// </list>
	///
	/// <para>Subclasses implement the four primitive clipboard operations:
	/// <see cref="DoCopyText"/>, <see cref="DoCopyData"/>,
	/// <see cref="DoGetText"/>, <see cref="DoClear"/>.  All public members of
	/// <see cref="IClipboardService"/> are provided by this class.</para>
	/// </summary>
	public abstract class ClipboardServiceBase : IClipboardService, IDisposable
	{
		// ── Ownership hash ─────────────────────────────────────────────────────

		private readonly object m_lock = new object();
		private byte[] m_ownerHash; // SHA-256 of last text written by us; null = not owner

		// ── Auto-clear timer ───────────────────────────────────────────────────

		private Timer m_timer;
		private bool m_timerActive;

		// ── IClipboardService — abstract primitives ────────────────────────────

		/// <summary>
		/// Writes <paramref name="text"/> to the platform clipboard.
		/// Called by <see cref="SetText"/> and <see cref="CopyText"/> after the
		/// ownership hash has been updated.
		/// </summary>
		protected abstract void DoCopyText(string text);

		/// <summary>
		/// Writes raw data in <paramref name="format"/> to the platform clipboard.
		/// Default implementation is a no-op; override for privacy markers.
		/// </summary>
		protected virtual void DoCopyData(string format, byte[] data) { }

		/// <summary>
		/// Reads the current text from the platform clipboard.
		/// Returns <c>null</c> if the clipboard is empty or holds non-text data.
		/// </summary>
		protected abstract string DoGetText();

		/// <summary>Empties the platform clipboard.</summary>
		protected abstract void DoClear();

		// ── IClipboardService implementation ───────────────────────────────────

		/// <inheritdoc/>
		public abstract bool IsSupported { get; }

		/// <inheritdoc/>
		public void SetText(string text)
		{
			if(text == null) throw new ArgumentNullException("text");
			lock(m_lock) { m_ownerHash = HashText(text); }
			DoCopyText(text);
		}

		/// <inheritdoc/>
		public string GetText() => DoGetText();

		/// <inheritdoc/>
		public void Clear()
		{
			lock(m_lock) { m_ownerHash = null; }
			StopAutoClearInternal();
			DoClear();
		}

		/// <inheritdoc/>
		public void ClearIfOwner()
		{
			byte[] hash;
			lock(m_lock) { hash = m_ownerHash; }
			if(hash == null) return;

			// Compare the stored hash against a hash of the current clipboard text.
			string current = DoGetText();
			if(current == null) { lock(m_lock) { m_ownerHash = null; } return; }

			byte[] currentHash = HashText(current);
			if(!BytesEqual(hash, currentHash)) return; // clipboard changed by another app

			lock(m_lock) { m_ownerHash = null; }
			StopAutoClearInternal();
			DoClear();
		}

		/// <inheritdoc/>
		public void SetWithAutoClear(string text, TimeSpan timeout)
		{
			SetText(text);
			if(timeout > TimeSpan.Zero)
				StartAutoClearInternal((int)timeout.TotalSeconds);
		}

		/// <inheritdoc/>
		public void CopyText(string text, bool setOwnership)
		{
			if(text == null) throw new ArgumentNullException("text");
			if(setOwnership)
				lock(m_lock) { m_ownerHash = HashText(text); }
			else
				lock(m_lock) { m_ownerHash = null; }
			DoCopyText(text);
		}

		/// <inheritdoc/>
		public void CopyData(string format, byte[] data)
		{
			if(format == null) throw new ArgumentNullException("format");
			DoCopyData(format, data);
		}

		/// <inheritdoc/>
		public void StartAutoClear(int seconds)
		{
			if(seconds <= 0) { StopAutoClearInternal(); return; }
			StartAutoClearInternal(seconds);
		}

		/// <inheritdoc/>
		public void StopAutoClear() => StopAutoClearInternal();

		/// <inheritdoc/>
		public bool IsAutoClearActive
		{
			get { lock(m_lock) { return m_timerActive; } }
		}

		// ── Auto-clear timer internals ─────────────────────────────────────────

		private void StartAutoClearInternal(int seconds)
		{
			lock(m_lock)
			{
				StopTimerLocked();
				m_timerActive = true;
				m_timer = new Timer(OnTimerTick, null,
					TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
			}
		}

		private void StopAutoClearInternal()
		{
			lock(m_lock) { StopTimerLocked(); }
		}

		private void StopTimerLocked()
		{
			m_timerActive = false;
			if(m_timer != null)
			{
				m_timer.Dispose();
				m_timer = null;
			}
		}

		private void OnTimerTick(object state)
		{
			lock(m_lock) { StopTimerLocked(); }
			ClearIfOwner();
		}

		// ── SHA-256 ownership hash ─────────────────────────────────────────────

		private static byte[] HashText(string text)
		{
			byte[] data = System.Text.Encoding.Unicode.GetBytes(text);
			return SHA256.HashData(data);
		}

		private static bool BytesEqual(byte[] a, byte[] b)
		{
			if(a == null || b == null) return a == b;
			if(a.Length != b.Length) return false;
			// Constant-time comparison to avoid timing side-channels.
			int diff = 0;
			for(int i = 0; i < a.Length; ++i) diff |= a[i] ^ b[i];
			return diff == 0;
		}

		// ── IDisposable ────────────────────────────────────────────────────────

		/// <summary>
		/// Disposes the auto-clear timer if one is running.
		/// Does not clear the clipboard.
		/// </summary>
		public void Dispose()
		{
			lock(m_lock) { StopTimerLocked(); }
		}
	}
}
