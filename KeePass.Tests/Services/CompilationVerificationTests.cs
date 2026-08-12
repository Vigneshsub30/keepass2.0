using System;
using System.IO;
using System.Xml.Linq;

using Xunit;

namespace KeePass.Tests.Services
{
    /// <summary>
    /// Structural tests verifying that project files conform to the
    /// System.Drawing isolation requirements introduced in WO-027.
    ///
    /// These tests are intentionally lightweight (they read XML, not compile
    /// code) and run on all CI platforms including Linux.
    /// </summary>
    public class CompilationVerificationTests
    {
        private static string FindRepoRoot()
        {
            // Walk up from the test assembly location until we find KeePass.sln.
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "KeePass.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException(
                "Could not locate repository root (no KeePass.sln found).");
        }

        /// <summary>
        /// KeePass.Core must not contain any PackageReference to
        /// System.Drawing.Common.  The cross-platform abstraction layer must
        /// stay free of all Windows-only image dependencies.
        /// </summary>
        [Fact]
        public void KeePassCore_Csproj_DoesNotReference_SystemDrawingCommon()
        {
            string root = FindRepoRoot();
            string csproj = Path.Combine(root, "KeePass.Core", "KeePass.Core.csproj");
            Assert.True(File.Exists(csproj), $"Project file not found: {csproj}");

            string xml = File.ReadAllText(csproj);
            Assert.False(
                xml.Contains("System.Drawing.Common", StringComparison.OrdinalIgnoreCase),
                "KeePass.Core.csproj must not reference System.Drawing.Common. " +
                "IImageService is a pure abstraction; platform-specific image libs " +
                "belong in KeePass (WinForms) or KeePass.Avalonia.");
        }

        /// <summary>
        /// KeePass.Core must target the cross-platform <c>net10.0</c> TFM,
        /// not the Windows-specific <c>net10.0-windows</c> TFM.
        /// </summary>
        [Fact]
        public void KeePassCore_Csproj_Targets_Net10_CrossPlatform()
        {
            string root = FindRepoRoot();
            string csproj = Path.Combine(root, "KeePass.Core", "KeePass.Core.csproj");
            Assert.True(File.Exists(csproj));

            XDocument doc = XDocument.Load(csproj);
            XNamespace ns = doc.Root.Name.Namespace;

            bool hasCrossPlatformTfm = false;
            foreach (XElement el in doc.Descendants("TargetFramework"))
            {
                string tfm = (string)el;
                // net10.0 is cross-platform; net10.0-windows is not.
                if (tfm != null &&
                    tfm.Equals("net10.0", StringComparison.OrdinalIgnoreCase))
                {
                    hasCrossPlatformTfm = true;
                    break;
                }
            }

            Assert.True(hasCrossPlatformTfm,
                "KeePass.Core must target 'net10.0' (not 'net10.0-windows') so that " +
                "its abstractions compile on Linux and macOS CI runners.");
        }

        /// <summary>
        /// The IImageService interface file must not import System.Drawing.
        /// </summary>
        [Fact]
        public void IImageService_SourceFile_DoesNotImport_SystemDrawing()
        {
            string root = FindRepoRoot();
            string src = Path.Combine(root, "KeePass.Core", "Services", "IImageService.cs");
            Assert.True(File.Exists(src), $"Source file not found: {src}");

            string code = File.ReadAllText(src);
            Assert.False(
                code.Contains("System.Drawing", StringComparison.Ordinal),
                "IImageService.cs must not reference System.Drawing. " +
                "It is a pure abstraction — platform image APIs belong in " +
                "concrete implementations in the head projects.");
        }

        /// <summary>
        /// The ImageData source file must not import System.Drawing.
        /// </summary>
        [Fact]
        public void ImageData_SourceFile_DoesNotImport_SystemDrawing()
        {
            string root = FindRepoRoot();
            string src = Path.Combine(root, "KeePass.Core", "Services", "ImageData.cs");
            Assert.True(File.Exists(src), $"Source file not found: {src}");

            string code = File.ReadAllText(src);
            Assert.False(
                code.Contains("System.Drawing", StringComparison.Ordinal),
                "ImageData.cs must not reference System.Drawing.");
        }

        /// <summary>
        /// The NullImageService source file must not import System.Drawing.
        /// </summary>
        [Fact]
        public void NullImageService_SourceFile_DoesNotImport_SystemDrawing()
        {
            string root = FindRepoRoot();
            string src = Path.Combine(root, "KeePass.Core", "Services", "NullImageService.cs");
            Assert.True(File.Exists(src), $"Source file not found: {src}");

            string code = File.ReadAllText(src);
            Assert.False(
                code.Contains("System.Drawing", StringComparison.Ordinal),
                "NullImageService.cs must not reference System.Drawing.");
        }

        /// <summary>
        /// FileFormatProvider.SmallIcon must return ImageData, not
        /// System.Drawing.Image, in its property declaration.
        /// Verified by text search since the WO changes the return type.
        /// </summary>
        [Fact]
        public void FileFormatProvider_SmallIcon_ReturnsImageData_NotImage()
        {
            string root = FindRepoRoot();
            string src = Path.Combine(root, "KeePass", "DataExchange", "FileFormatProvider.cs");
            Assert.True(File.Exists(src), $"Source file not found: {src}");

            string code = File.ReadAllText(src);
            Assert.Contains("ImageData SmallIcon", code,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Image SmallIcon", code,
                StringComparison.Ordinal);
        }
    }
}
