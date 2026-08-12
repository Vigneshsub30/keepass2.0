using System;
using System.IO;
using System.Security;

using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.SmokeTest;

using Xunit;

namespace KeePass.Tests.SmokeTest
{
	/// <summary>
	/// Unit tests for <see cref="SmokeTestRunner"/> exit codes and
	/// <see cref="KdbxRoundTripVerifier"/> behavior.
	///
	/// These tests exercise the wiring and exit-code semantics without
	/// needing the full KeePass application to be launched.
	/// </summary>
	public sealed class SmokeTestRunnerTests : IDisposable
	{
		private readonly string m_tempDir;

		public SmokeTestRunnerTests()
		{
			m_tempDir = Path.Combine(Path.GetTempPath(),
				"KeePassTests_Smoke_" + Path.GetRandomFileName());
			Directory.CreateDirectory(m_tempDir);
		}

		public void Dispose()
		{
			try { Directory.Delete(m_tempDir, recursive: true); }
			catch { /* best-effort */ }
		}

		// ── SmokeTestRunner exit-code constants ───────────────────────────────── //

		[Fact]
		public void ExitCodes_HaveExpectedValues()
		{
			Assert.Equal(0, SmokeTestRunner.ExitSuccess);
			Assert.Equal(1, SmokeTestRunner.ExitSelfTestFailed);
			Assert.Equal(2, SmokeTestRunner.ExitRoundTripFailed);
			Assert.Equal(3, SmokeTestRunner.ExitFixtureFailed);
		}

		[Fact]
		public void ExitCodes_AreDistinct()
		{
			int[] codes =
			{
				SmokeTestRunner.ExitSuccess,
				SmokeTestRunner.ExitSelfTestFailed,
				SmokeTestRunner.ExitRoundTripFailed,
				SmokeTestRunner.ExitFixtureFailed,
			};
			Assert.Equal(codes.Length, new System.Collections.Generic.HashSet<int>(codes).Count);
		}

		// ── SelfTest.Perform() returns expected test count ────────────────────── //

		[Fact]
		public void SelfTest_Perform_ReturnsExpectedCount()
		{
			int n = SelfTest.Perform();
			Assert.Equal(SelfTest.ExpectedTestCount, n);
		}

		[Fact]
		public void SelfTest_ExpectedTestCount_IsPositive()
		{
			Assert.True(SelfTest.ExpectedTestCount > 0);
		}

		// ── KdbxRoundTripVerifier — happy paths ───────────────────────────────── //

		[Fact]
		public void RoundTripVerifier_ValidVault_DoesNotThrow()
		{
			string path = WriteMinimalVault("valid.kdbx", "RoundTripPass1!");
			KdbxRoundTripVerifier.Verify(path, "RoundTripPass1!");
		}

		[Fact]
		public void RoundTripVerifier_MultipleEntries_DoesNotThrow()
		{
			string path = WriteVaultWithMultipleEntries("multi.kdbx", "MultiPass1!");
			KdbxRoundTripVerifier.Verify(path, "MultiPass1!");
		}

		// ── KdbxRoundTripVerifier — failure paths ─────────────────────────────── //

		[Fact]
		public void RoundTripVerifier_MissingFile_ThrowsFileNotFound()
		{
			string path = Path.Combine(m_tempDir, "nonexistent.kdbx");
			Assert.Throws<FileNotFoundException>(
				() => KdbxRoundTripVerifier.Verify(path, "anyPassword"));
		}

		[Fact]
		public void RoundTripVerifier_WrongPassword_ThrowsException()
		{
			string path = WriteMinimalVault("wrongpw.kdbx", "CorrectPassword1!");
			Assert.ThrowsAny<Exception>(
				() => KdbxRoundTripVerifier.Verify(path, "WrongPassword1!"));
		}

		[Fact]
		public void RoundTripVerifier_NullPath_ThrowsArgumentNull()
		{
			Assert.Throws<ArgumentNullException>(
				() => KdbxRoundTripVerifier.Verify(null!, "password"));
		}

		[Fact]
		public void RoundTripVerifier_EmptyPath_ThrowsArgumentNull()
		{
			Assert.Throws<ArgumentNullException>(
				() => KdbxRoundTripVerifier.Verify(string.Empty, "password"));
		}

		// ── SmokeTestRunner.Run() — integration ───────────────────────────────── //

		[Fact]
		public void SmokeTestRunner_Run_ReturnsExitSuccess()
		{
			int exit = SmokeTestRunner.Run();
			Assert.Equal(SmokeTestRunner.ExitSuccess, exit);
		}

		// ── Helpers ───────────────────────────────────────────────────────────── //

		private string WriteMinimalVault(string name, string password)
		{
			PwDatabase db  = new PwDatabase();
			CompositeKey key = new CompositeKey();
			key.AddUserKey(new KcpPassword(password));
			db.MasterKey = key;

			PwGroup root = new PwGroup(true, true, "Root", PwIcon.Folder);
			db.RootGroup = root;

			PwEntry e = new PwEntry(true, true);
			e.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Entry-1"));
			e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "pass!"));
			root.AddEntry(e, true);

			// Use AES-KDF with low rounds so the test is fast
			var aesKdf = new KeePassLib.Cryptography.KeyDerivation.AesKdf();
			var kdfP = aesKdf.GetDefaultParameters();
			kdfP.SetUInt64(KeePassLib.Cryptography.KeyDerivation.AesKdf.ParamRounds, 1000);
			db.KdfParameters = kdfP;

			string path = Path.Combine(m_tempDir, name);
			using(FileStream fs = new FileStream(path, FileMode.Create,
				FileAccess.Write, FileShare.None))
			{
				new KdbxFile(db).Save(fs, null, KdbxFormat.Default, null);
			}
			return path;
		}

		private string WriteVaultWithMultipleEntries(string name, string password)
		{
			PwDatabase db  = new PwDatabase();
			CompositeKey key = new CompositeKey();
			key.AddUserKey(new KcpPassword(password));
			db.MasterKey = key;

			PwGroup root  = new PwGroup(true, true, "Root",  PwIcon.Folder);
			PwGroup sub   = new PwGroup(true, true, "Sub",   PwIcon.Folder);
			db.RootGroup = root;
			root.AddGroup(sub, true);

			for(int i = 0; i < 5; ++i)
			{
				PwEntry e = new PwEntry(true, true);
				e.Strings.Set(PwDefs.TitleField,
					new ProtectedString(false, $"Entry-{i}"));
				e.Strings.Set(PwDefs.PasswordField,
					new ProtectedString(true, $"pass{i}!"));
				(i % 2 == 0 ? root : sub).AddEntry(e, true);
			}

			var aesKdf = new KeePassLib.Cryptography.KeyDerivation.AesKdf();
			var kdfP = aesKdf.GetDefaultParameters();
			kdfP.SetUInt64(KeePassLib.Cryptography.KeyDerivation.AesKdf.ParamRounds, 1000);
			db.KdfParameters = kdfP;

			string path = Path.Combine(m_tempDir, name);
			using(FileStream fs = new FileStream(path, FileMode.Create,
				FileAccess.Write, FileShare.None))
			{
				new KdbxFile(db).Save(fs, null, KdbxFormat.Default, null);
			}
			return path;
		}
	}
}
