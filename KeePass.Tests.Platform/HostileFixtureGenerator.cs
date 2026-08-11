#nullable enable

using System;
using System.IO;
using System.Text;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Generates hostile/adversarial import fixture streams programmatically so
	/// no binary fixtures need to be committed to the repository.
	/// </summary>
	internal static class HostileFixtureGenerator
	{
		// ── XML attacks ───────────────────────────────────────────────── //

		/// <summary>
		/// Classic "billion laughs" XML entity-expansion bomb.
		/// </summary>
		public static Stream XmlBillionLaughs() => Utf8Stream(@"<?xml version=""1.0""?>
<!DOCTYPE lolz [
  <!ENTITY lol ""lol"">
  <!ENTITY lol2 ""&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;"">
  <!ENTITY lol3 ""&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;"">
  <!ENTITY lol4 ""&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;"">
]>
<root>&lol4;</root>");

		/// <summary>
		/// XML with an external entity reference (XXE attack vector).
		/// </summary>
		public static Stream XmlExternalEntity() => Utf8Stream(@"<?xml version=""1.0""?>
<!DOCTYPE foo [ <!ENTITY xxe SYSTEM ""file:///etc/passwd""> ]>
<root>&xxe;</root>");

		/// <summary>
		/// XML nested to exactly <paramref name="depth"/> levels.
		/// </summary>
		public static Stream XmlDeepNesting(int depth)
		{
			var sb = new StringBuilder("<?xml version=\"1.0\"?>");
			for(int i = 0; i < depth; i++) sb.Append("<x>");
			sb.Append("deep");
			for(int i = 0; i < depth; i++) sb.Append("</x>");
			return Utf8Stream(sb.ToString());
		}

		/// <summary>
		/// Quadratic-blowup XML: deeply nested CDATA that expands quadratically.
		/// </summary>
		public static Stream XmlQuadraticBlowup()
		{
			var sb = new StringBuilder("<?xml version=\"1.0\"?><root>");
			for(int i = 0; i < 50; i++)
				sb.Append("<item><![CDATA[" + new string('A', 1000) + "]]></item>");
			sb.Append("</root>");
			return Utf8Stream(sb.ToString());
		}

		// ── Oversized stream ──────────────────────────────────────────── //

		/// <summary>
		/// A fake stream that reports <paramref name="totalSize"/> bytes without
		/// allocating them in memory.  Used to test size-limit enforcement.
		/// </summary>
		public static Stream OversizedStream(long totalSize) =>
			new FakeOversizedStream(totalSize);

		private sealed class FakeOversizedStream : Stream
		{
			private readonly long _total;
			private long _pos;

			public FakeOversizedStream(long total)
			{
				if(total < 0) throw new ArgumentOutOfRangeException(nameof(total));
				_total = total;
			}

			public override bool CanRead  => true;
			public override bool CanSeek  => false;
			public override bool CanWrite => false;
			public override long Length   => _total;
			public override long Position
			{
				get => _pos;
				set => throw new NotSupportedException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				long remaining = _total - _pos;
				if(remaining <= 0) return 0;
				int n = (int)Math.Min(count, remaining);
				Array.Clear(buffer, offset, n);
				_pos += n;
				return n;
			}

			public override void Flush() { }
			public override long Seek(long o, SeekOrigin r) => throw new NotSupportedException();
			public override void SetLength(long v) => throw new NotSupportedException();
			public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
		}

		// ── CSV / text attacks ────────────────────────────────────────── //

		/// <summary>
		/// A CSV stream with <paramref name="rowCount"/> data rows (title header + data).
		/// </summary>
		public static Stream CsvWithRowCount(int rowCount)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Title,Username,Password,URL,Notes");
			for(int i = 0; i < rowCount; i++)
				sb.AppendLine($"Entry{i},user{i},pass{i},https://ex.com,note{i}");
			return Utf8Stream(sb.ToString());
		}

		/// <summary>
		/// A text file containing null bytes at various positions.
		/// </summary>
		public static Stream NullByteText()
		{
			byte[] data = Encoding.UTF8.GetBytes("<root>\0<item\0/></root\0>");
			return new MemoryStream(data);
		}

		// ── Binary / KDBX ────────────────────────────────────────────── //

		/// <summary>
		/// A zero-byte (empty) stream.
		/// </summary>
		public static Stream ZeroByte() => new MemoryStream(Array.Empty<byte>());

		/// <summary>
		/// A stream with a corrupted KDBX header (wrong magic signature).
		/// </summary>
		public static Stream CorruptedKdbxHeader()
		{
			byte[] data = new byte[32];
			// KDBX magic: 0x9AA2D903, 0xB54BFB67 — corrupt it
			data[0] = 0xDE; data[1] = 0xAD; data[2] = 0xBE; data[3] = 0xEF;
			data[4] = 0xDE; data[5] = 0xAD; data[6] = 0xBE; data[7] = 0xEF;
			return new MemoryStream(data);
		}

		// ── JSON attacks ──────────────────────────────────────────────── //

		/// <summary>
		/// A deeply nested JSON object.
		/// </summary>
		public static Stream JsonDeepNesting(int depth)
		{
			var sb = new StringBuilder();
			for(int i = 0; i < depth; i++) sb.Append("{\"a\":");
			sb.Append("\"leaf\"");
			for(int i = 0; i < depth; i++) sb.Append('}');
			return Utf8Stream(sb.ToString());
		}

		// ── Helpers ───────────────────────────────────────────────────── //

		private static Stream Utf8Stream(string content) =>
			new MemoryStream(Encoding.UTF8.GetBytes(content));
	}
}
