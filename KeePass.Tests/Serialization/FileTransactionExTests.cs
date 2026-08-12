using System;
using System.IO;
using System.Text;
using KeePassLib.Serialization;
using Xunit;

namespace KeePass.Tests.Serialization
{
    /// <summary>
    /// Integration tests for FileTransactionEx — the atomic write-commit mechanism
    /// used when saving KDBX files.
    ///
    /// Each test runs against real local file system operations in a per-test
    /// temporary directory that is deleted after the test.  Tests are cross-platform
    /// (FileTransactionEx uses a simple-rename fallback on Unix).
    /// </summary>
    public class FileTransactionExTests : IDisposable
    {
        private readonly string m_tempDir;

        public FileTransactionExTests()
        {
            m_tempDir = Path.Combine(Path.GetTempPath(),
                $"keepass-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_tempDir, true); }
            catch { /* best-effort cleanup */ }
        }

        // ── 1. Happy path: write → commit → verify ────────────────────────────

        [Fact]
        public void CommitWrite_TransactedMode_ReplacesFileContent()
        {
            string path = CreateTempFile("original content");
            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);

            using (FileTransactionEx tx = new FileTransactionEx(ioc, true))
            {
                using (Stream s = tx.OpenWrite())
                    WriteText(s, "new content");
                tx.CommitWrite();
            }

            Assert.Equal("new content", File.ReadAllText(path, Encoding.UTF8));
        }

        [Fact]
        public void CommitWrite_NonTransactedMode_ReplacesFileContent()
        {
            string path = CreateTempFile("original-non-transacted");
            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);

            using (FileTransactionEx tx = new FileTransactionEx(ioc, false))
            {
                using (Stream s = tx.OpenWrite())
                    WriteText(s, "updated-non-transacted");
                tx.CommitWrite();
            }

            Assert.Equal("updated-non-transacted", File.ReadAllText(path, Encoding.UTF8));
        }

        // ── 2. Abort path: dispose without commit ────────────────────────────

        [Fact]
        public void Dispose_WithoutCommit_PreservesOriginalFile()
        {
            string path = CreateTempFile("preserve-me");
            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);

            // Write but do NOT call CommitWrite
            using (FileTransactionEx tx = new FileTransactionEx(ioc, true))
            {
                using (Stream s = tx.OpenWrite())
                    WriteText(s, "should-not-persist");
                // tx.CommitWrite() intentionally not called
            }

            Assert.Equal("preserve-me", File.ReadAllText(path, Encoding.UTF8));
        }

        // ── 3. New file creation ──────────────────────────────────────────────

        [Fact]
        public void CommitWrite_NewFile_CreatesFileWithCorrectContent()
        {
            string path = Path.Combine(m_tempDir, "new-file.kdbx");
            Assert.False(File.Exists(path));

            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);
            using (FileTransactionEx tx = new FileTransactionEx(ioc, true))
            {
                using (Stream s = tx.OpenWrite())
                    WriteText(s, "brand-new-content");
                tx.CommitWrite();
            }

            Assert.True(File.Exists(path));
            Assert.Equal("brand-new-content", File.ReadAllText(path, Encoding.UTF8));
        }

        // ── 4. Binary content fidelity ────────────────────────────────────────

        [Fact]
        public void CommitWrite_BinaryContent_IsPreservedExactly()
        {
            byte[] binaryData = new byte[256];
            for (int i = 0; i < 256; ++i)
                binaryData[i] = (byte)i;

            string path = CreateTempFile(string.Empty);
            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);

            using (FileTransactionEx tx = new FileTransactionEx(ioc, true))
            {
                using (Stream s = tx.OpenWrite())
                    s.Write(binaryData, 0, binaryData.Length);
                tx.CommitWrite();
            }

            byte[] read = File.ReadAllBytes(path);
            Assert.Equal(binaryData, read);
        }

        // ── 5. Dispose cleans up temp files ──────────────────────────────────

        [Fact]
        public void Dispose_WithoutCommit_CleansTempFiles()
        {
            string path = CreateTempFile("clean-up");
            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);

            // Count files in temp dir before
            int countBefore = Directory.GetFiles(m_tempDir).Length;

            using (FileTransactionEx tx = new FileTransactionEx(ioc, true))
            {
                using (Stream s = tx.OpenWrite())
                    WriteText(s, "data");
                // No commit — temp file should be removed on Dispose
            }

            int countAfter = Directory.GetFiles(m_tempDir).Length;

            // After Dispose, temp files should be cleaned up (count must not increase)
            Assert.True(countAfter <= countBefore,
                $"Expected temp file cleanup; files before={countBefore}, after={countAfter}");
        }

        // ── 6. Overwrite existing large file ─────────────────────────────────

        [Fact]
        public void CommitWrite_LargeFile_ReplacedCorrectly()
        {
            byte[] original = new byte[50 * 1024];  // 50 KB original
            for (int i = 0; i < original.Length; ++i)
                original[i] = (byte)(i % 251);

            byte[] replacement = new byte[30 * 1024];  // 30 KB replacement
            for (int i = 0; i < replacement.Length; ++i)
                replacement[i] = (byte)(i % 127);

            string path = Path.Combine(m_tempDir, "large.bin");
            File.WriteAllBytes(path, original);

            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);
            using (FileTransactionEx tx = new FileTransactionEx(ioc, true))
            {
                using (Stream s = tx.OpenWrite())
                    s.Write(replacement, 0, replacement.Length);
                tx.CommitWrite();
            }

            byte[] read = File.ReadAllBytes(path);
            Assert.Equal(replacement.Length, read.Length);
            Assert.Equal(replacement, read);
        }

        // ── 7. Multiple sequential transactions on same file ─────────────────

        [Fact]
        public void CommitWrite_SequentialTransactions_EachUpdateTakeEffect()
        {
            string path = CreateTempFile("v0");
            IOConnectionInfo ioc = IOConnectionInfo.FromPath(path);

            for (int v = 1; v <= 3; ++v)
            {
                string content = $"v{v}";
                using (FileTransactionEx tx = new FileTransactionEx(ioc, true))
                {
                    using (Stream s = tx.OpenWrite())
                        WriteText(s, content);
                    tx.CommitWrite();
                }
                Assert.Equal(content, File.ReadAllText(path, Encoding.UTF8));
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string CreateTempFile(string content)
        {
            string path = Path.Combine(m_tempDir, $"{Guid.NewGuid():N}.bin");
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }

        private static void WriteText(Stream s, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            s.Write(bytes, 0, bytes.Length);
        }
    }
}
