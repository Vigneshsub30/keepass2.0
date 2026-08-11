#nullable enable

using System;
using System.Runtime.InteropServices;

using KeePassLib.Utility;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="MemUtil.SecureZero(Span{byte})"/> and
	/// <see cref="MemUtil.SecureZero{T}(Span{T})"/>.
	/// Verifies that after the call every element in the buffer is zero,
	/// across a range of sizes and element types.
	/// </summary>
	public sealed class SecureZeroTests
	{
		// ── byte[] ────────────────────────────────────────────────────── //

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(32)]
		[InlineData(1024)]
		public void SecureZero_ByteSpan_AllZeroAfterCall(int size)
		{
			byte[] buf = new byte[size];
			for(int i = 0; i < size; i++) buf[i] = (byte)(i % 256 + 1); // non-zero

			MemUtil.SecureZero(buf.AsSpan());

			foreach(byte b in buf)
				Assert.Equal(0, b);
		}

		[Fact]
		public void SecureZero_ByteSpan_OneMegabyte_AllZero()
		{
			const int mb = 1 << 20; // 1 MiB
			byte[] buf = new byte[mb];
			for(int i = 0; i < mb; i++) buf[i] = 0xFF;

			MemUtil.SecureZero(buf.AsSpan());

			foreach(byte b in buf)
				Assert.Equal(0, b);
		}

		[Fact]
		public void SecureZero_EmptyByteSpan_DoesNotThrow()
		{
			var ex = Record.Exception(() => MemUtil.SecureZero(Span<byte>.Empty));
			Assert.Null(ex);
		}

		// ── ulong[] ───────────────────────────────────────────────────── //

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(32)]
		[InlineData(1024)]
		public void SecureZero_UlongSpan_AllZeroAfterCall(int count)
		{
			ulong[] buf = new ulong[count];
			for(int i = 0; i < count; i++) buf[i] = ulong.MaxValue;

			MemUtil.SecureZero<ulong>(buf.AsSpan());

			foreach(ulong v in buf)
				Assert.Equal(0UL, v);
		}

		[Fact]
		public void SecureZero_UlongSpan_OneMegabyte_AllZero()
		{
			const int count = (1 << 20) / sizeof(ulong); // 1 MiB worth of ulongs
			ulong[] buf = new ulong[count];
			Array.Fill(buf, ulong.MaxValue);

			MemUtil.SecureZero<ulong>(buf.AsSpan());

			foreach(ulong v in buf)
				Assert.Equal(0UL, v);
		}

		[Fact]
		public void SecureZero_EmptyUlongSpan_DoesNotThrow()
		{
			var ex = Record.Exception(() => MemUtil.SecureZero<ulong>(Span<ulong>.Empty));
			Assert.Null(ex);
		}

		// ── char[] ────────────────────────────────────────────────────── //

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(32)]
		[InlineData(1024)]
		public void SecureZero_CharSpan_AllZeroAfterCall(int count)
		{
			char[] buf = new char[count];
			for(int i = 0; i < count; i++) buf[i] = (char)(i % 0xD7FF + 1);

			MemUtil.SecureZero<char>(buf.AsSpan());

			foreach(char c in buf)
				Assert.Equal('\0', c);
		}

		// ── Idempotency ────────────────────────────────────────────────── //

		[Fact]
		public void SecureZero_CalledTwice_StillAllZero()
		{
			byte[] buf = { 0xDE, 0xAD, 0xBE, 0xEF };
			MemUtil.SecureZero(buf.AsSpan());
			MemUtil.SecureZero(buf.AsSpan());
			Assert.All(buf, b => Assert.Equal(0, b));
		}

		// ── Backward-compat delegation check ──────────────────────────── //

		[Fact]
#pragma warning disable CS0618
		public void ZeroByteArray_DelegatesTo_SecureZero()
		{
			byte[] buf = { 1, 2, 3, 4 };
			MemUtil.ZeroByteArray(buf);
			Assert.All(buf, b => Assert.Equal(0, b));
		}
#pragma warning restore CS0618
	}
}
