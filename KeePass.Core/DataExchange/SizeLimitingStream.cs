using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KeePass.Core.DataExchange
{
	/// <summary>
	/// A read-only <see cref="Stream"/> wrapper that counts cumulative bytes
	/// read and throws <see cref="ImportValidationException"/> the moment the
	/// configured <see cref="ImportValidationOptions.MaxFileSize"/> ceiling is
	/// exceeded.
	/// </summary>
	public sealed class SizeLimitingStream : Stream
	{
		private readonly Stream _inner;
		private readonly long _maxBytes;
		private long _totalRead;

		/// <summary>
		/// Initialises the wrapper.
		/// </summary>
		/// <param name="inner">Underlying stream to read from (not disposed
		///   when this stream is disposed).</param>
		/// <param name="maxBytes">Maximum cumulative bytes allowed. Reading
		///   past this value throws <see cref="ImportValidationException"/>.</param>
		public SizeLimitingStream(Stream inner, long maxBytes)
		{
			if(inner is null) throw new ArgumentNullException(nameof(inner));
			if(maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
			_inner    = inner;
			_maxBytes = maxBytes;
		}

		// ── Stream capability ──────────────────────────────────────── //

		public override bool CanRead  => _inner.CanRead;
		public override bool CanSeek  => false;
		public override bool CanWrite => false;
		public override long Length   => throw new NotSupportedException();
		public override long Position
		{
			get => _totalRead;
			set => throw new NotSupportedException();
		}

		// ── Read ───────────────────────────────────────────────────── //

		public override int Read(byte[] buffer, int offset, int count)
		{
			int n = _inner.Read(buffer, offset, count);
			AccountFor(n);
			return n;
		}

		public override int Read(Span<byte> buffer)
		{
			int n = _inner.Read(buffer);
			AccountFor(n);
			return n;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset,
			int count, CancellationToken cancellationToken)
		{
			int n = await _inner.ReadAsync(buffer, offset, count, cancellationToken)
				.ConfigureAwait(false);
			AccountFor(n);
			return n;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
			CancellationToken cancellationToken = default)
		{
			int n = await _inner.ReadAsync(buffer, cancellationToken)
				.ConfigureAwait(false);
			AccountFor(n);
			return n;
		}

		public override int ReadByte()
		{
			int b = _inner.ReadByte();
			if(b >= 0) AccountFor(1);
			return b;
		}

		// ── Unsupported write / seek ───────────────────────────────── //

		public override void Write(byte[] buffer, int offset, int count)
			=> throw new NotSupportedException();

		public override void Flush() { /* nothing to flush — read-only wrapper */ }

		public override long Seek(long offset, SeekOrigin origin)
			=> throw new NotSupportedException();

		public override void SetLength(long value)
			=> throw new NotSupportedException();

		// ── Internal accounting ────────────────────────────────────── //

		private void AccountFor(int bytesJustRead)
		{
			if(bytesJustRead <= 0) return;
			_totalRead += bytesJustRead;
			if(_totalRead > _maxBytes)
				throw new ImportValidationException(
					nameof(ImportValidationOptions.MaxFileSize),
					_maxBytes,
					_totalRead);
		}

		protected override void Dispose(bool disposing)
		{
			// Intentionally do NOT dispose _inner — the caller owns its lifetime.
			base.Dispose(disposing);
		}
	}
}
