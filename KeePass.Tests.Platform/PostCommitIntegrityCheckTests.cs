using System;
using System.IO;
using System.Text;

using KeePassLib.Resources;
using KeePassLib.Serialization;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Unit tests for <see cref="FileTransactionEx.PostCommitIntegrityCheck"/>.
	///
	/// The method is <c>internal</c> and exposed to this test assembly via
	/// <c>InternalsVisibleTo</c> in KeePassLib.
	/// </summary>
	public sealed class PostCommitIntegrityCheckTests : IDisposable
	{
		private readonly string m_tempDir;

		public PostCommitIntegrityCheckTests()
		{
			m_tempDir = Path.Combine(Path.GetTempPath(),
				"KeePassTests_" + Path.GetRandomFileName());
			Directory.CreateDirectory(m_tempDir);
		}

		public void Dispose()
		{
			try { Directory.Delete(m_tempDir, recursive: true); }
			catch { /* best-effort cleanup */ }
		}

		// ── helpers ──────────────────────────────────────────────────────── //

		private string WriteTempFile(string name, byte[] contents)
		{
			string path = Path.Combine(m_tempDir, name);
			File.WriteAllBytes(path, contents);
			return path;
		}

		private static byte[] KdbxHeaderBytes(int totalLength = 64)
		{
			// KDBX primary signature: 0x9AA2D903 (LE)
			// KDBX secondary signature: 0xB54BFB67 (LE)
			// Followed by version bytes and zero padding
			var buf = new byte[totalLength];
			buf[0] = 0x03; buf[1] = 0xD9; buf[2] = 0xA2; buf[3] = 0x9A; // primary sig
			buf[4] = 0x67; buf[5] = 0xFB; buf[6] = 0x4B; buf[7] = 0xB5; // secondary sig
			buf[8] = 0x01; buf[9] = 0x00; buf[10] = 0x04; buf[11] = 0x00; // version 4.1
			return buf;
		}

		private static byte[] BadHeaderBytes(int totalLength = 64)
		{
			// Plausible-length file but wrong signature bytes
			var buf = new byte[totalLength];
			buf[0] = 0xFF; buf[1] = 0xFF; buf[2] = 0xFF; buf[3] = 0xFF;
			return buf;
		}

		// ── KLRes message tests ───────────────────────────────────────────── //

		[Fact]
		public void KLRes_VaultFileMissingAfterSave_ContainsFormatPlaceholder()
		{
			string msg = KLRes.VaultFileMissingAfterSave;
			Assert.Contains("{0}", msg);
		}

		[Fact]
		public void KLRes_VaultFileCorruptAfterSave_ContainsFormatPlaceholder()
		{
			string msg = KLRes.VaultFileCorruptAfterSave;
			Assert.Contains("{0}", msg);
		}

		[Fact]
		public void KLRes_VaultFileMissingAfterSave_FormatsPath()
		{
			const string path = @"C:\vaults\passwords.kdbx";
			string msg = string.Format(KLRes.VaultFileMissingAfterSave, path);
			Assert.Contains(path, msg);
		}

		[Fact]
		public void KLRes_VaultFileCorruptAfterSave_FormatsPath()
		{
			const string path = @"/home/user/.keepass/passwords.kdbx";
			string msg = string.Format(KLRes.VaultFileCorruptAfterSave, path);
			Assert.Contains(path, msg);
		}

		// ── PostCommitIntegrityCheck — success paths ──────────────────────── //

		[Fact]
		public void Check_ValidKdbxFile_DoesNotThrow()
		{
			string path = WriteTempFile("valid.kdbx", KdbxHeaderBytes());
			FileTransactionEx.PostCommitIntegrityCheck(path); // must not throw
		}

		[Fact]
		public void Check_NonKdbxExtension_WithAnyContent_DoesNotThrow()
		{
			// For non-.kdbx files the signature check is skipped; only existence
			// and minimum-length are verified.
			byte[] content = Encoding.UTF8.GetBytes("<KeePassFile>...</KeePassFile>");
			string path = WriteTempFile("export.xml", content);
			FileTransactionEx.PostCommitIntegrityCheck(path); // must not throw
		}

		[Fact]
		public void Check_LargeValidKdbxFile_DoesNotThrow()
		{
			// Large files should only read the first 12 bytes, not the whole file.
			var buf = KdbxHeaderBytes(totalLength: 4096);
			string path = WriteTempFile("large.kdbx", buf);
			FileTransactionEx.PostCommitIntegrityCheck(path); // must not throw
		}

		[Fact]
		public void Check_KdbFile_WithValidPrimarySignature_DoesNotThrow()
		{
			// .kdb (legacy KDB) uses the same primary signature but a different
			// secondary signature.  PostCommitIntegrityCheck only verifies the
			// primary signature word, so a valid primary is sufficient.
			var buf = new byte[64];
			buf[0] = 0x03; buf[1] = 0xD9; buf[2] = 0xA2; buf[3] = 0x9A;
			buf[4] = 0x65; buf[5] = 0xFB; buf[6] = 0x4B; buf[7] = 0xB5; // KDB secondary
			string path = WriteTempFile("legacy.kdb", buf);
			FileTransactionEx.PostCommitIntegrityCheck(path); // must not throw
		}

		// ── PostCommitIntegrityCheck — failure paths ──────────────────────── //

		[Fact]
		public void Check_MissingFile_Throws()
		{
			string path = Path.Combine(m_tempDir, "does_not_exist.kdbx");
			var ex = Assert.Throws<InvalidOperationException>(
				() => FileTransactionEx.PostCommitIntegrityCheck(path));
			Assert.Contains(path, ex.Message);
		}

		[Fact]
		public void Check_EmptyFile_Throws()
		{
			string path = WriteTempFile("empty.kdbx", Array.Empty<byte>());
			var ex = Assert.Throws<InvalidOperationException>(
				() => FileTransactionEx.PostCommitIntegrityCheck(path));
			Assert.Contains(path, ex.Message);
		}

		[Fact]
		public void Check_TooShortFile_Throws()
		{
			// 11 bytes — one byte less than the required 12-byte minimum
			string path = WriteTempFile("short.kdbx", new byte[11]);
			var ex = Assert.Throws<InvalidOperationException>(
				() => FileTransactionEx.PostCommitIntegrityCheck(path));
			Assert.Contains(path, ex.Message);
		}

		[Fact]
		public void Check_KdbxFileWithWrongSignature_Throws()
		{
			string path = WriteTempFile("corrupt.kdbx", BadHeaderBytes());
			var ex = Assert.Throws<InvalidOperationException>(
				() => FileTransactionEx.PostCommitIntegrityCheck(path));
			Assert.Contains(path, ex.Message);
		}

		[Fact]
		public void Check_ZeroByteKdbxFile_Throws()
		{
			// A zero-byte file is the classic "write-back cache lie" scenario.
			string path = WriteTempFile("zerobyte.kdbx", new byte[0]);
			var ex = Assert.Throws<InvalidOperationException>(
				() => FileTransactionEx.PostCommitIntegrityCheck(path));
			Assert.Contains(path, ex.Message);
		}

		[Fact]
		public void Check_MissingFile_ExceptionMessageContainsRecoveryHint()
		{
			string path = Path.Combine(m_tempDir, "gone.kdbx");
			var ex = Assert.Throws<InvalidOperationException>(
				() => FileTransactionEx.PostCommitIntegrityCheck(path));
			// The message must mention either "backup" or "recovery" so the user
			// knows what to do next.
			string lower = ex.Message.ToLowerInvariant();
			Assert.True(lower.Contains("backup") || lower.Contains("recovery"),
				$"Expected recovery hint in: {ex.Message}");
		}

		[Fact]
		public void Check_CorruptKdbxFile_ExceptionMessageContainsRecoveryHint()
		{
			string path = WriteTempFile("corrupt2.kdbx", BadHeaderBytes());
			var ex = Assert.Throws<InvalidOperationException>(
				() => FileTransactionEx.PostCommitIntegrityCheck(path));
			string lower = ex.Message.ToLowerInvariant();
			Assert.True(lower.Contains("backup") || lower.Contains("recovery"),
				$"Expected recovery hint in: {ex.Message}");
		}
	}
}
