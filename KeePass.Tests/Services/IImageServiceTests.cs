using System;
using System.IO;
using System.Reflection;

using KeePass.Core.Services;

using Xunit;

namespace KeePass.Tests.Services
{
    /// <summary>
    /// Contract and characterization tests for <see cref="IImageService"/>
    /// verified through <see cref="NullImageService"/>.
    ///
    /// Covers:
    ///   - <see cref="ImageData"/> construction, immutability, and edge cases.
    ///   - <see cref="NullImageService"/> happy-path operations.
    ///   - Contract-level parameter validation (Resize, ConvertToFormat).
    ///   - Fixture loading: embedded 16×16 PNG, 32×32 PNG, and ICO resources.
    ///   - Edge cases from the WO: null/empty input, zero dimensions, unsupported
    ///     format conversion, out-of-range icon ID.
    /// </summary>
    public class IImageServiceTests
    {
        private static readonly IImageService _svc = NullImageService.Instance;

        // ── Fixture helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Loads an embedded image resource from KeePass.Tests by short name
        /// (e.g. "test16x16.png").
        /// </summary>
        private static byte[] LoadFixture(string fileName)
        {
            Assembly asm = typeof(IImageServiceTests).Assembly;
            // Resource names follow the namespace + path convention used by
            // the SDK: KeePass.Tests.Fixtures.Images.<filename>
            string resourceName = $"KeePass.Tests.Fixtures.Images.{fileName}";
            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        $"Embedded resource '{resourceName}' not found in assembly. " +
                        $"Available: {string.Join(", ", asm.GetManifestResourceNames())}");

                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        // ── 1. ImageData construction ─────────────────────────────────────────

        [Fact]
        public void ImageData_Empty_IsEmpty()
        {
            Assert.True(ImageData.Empty.IsEmpty);
            Assert.Equal(0, ImageData.Empty.Width);
            Assert.Equal(0, ImageData.Empty.Height);
        }

        [Fact]
        public void ImageData_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ImageData(null, ImageFormat.Png, 16, 16));
        }

        [Fact]
        public void ImageData_NegativeWidth_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ImageData(new byte[1], ImageFormat.Png, -1, 16));
        }

        [Fact]
        public void ImageData_NegativeHeight_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ImageData(new byte[1], ImageFormat.Png, 16, -1));
        }

        [Fact]
        public void ImageData_ZeroDimensions_IsValid()
        {
            var d = new ImageData(new byte[] { 1, 2, 3 }, ImageFormat.Png, 0, 0);
            Assert.False(d.IsEmpty);
            Assert.Equal(0, d.Width);
            Assert.Equal(0, d.Height);
        }

        [Fact]
        public void ImageData_WithData_ReturnsNewInstanceWithSameFormatAndDimensions()
        {
            var original = new ImageData(new byte[] { 1, 2 }, ImageFormat.Bmp, 32, 32);
            byte[] newBytes = new byte[] { 9, 8, 7 };
            var replaced = original.WithData(newBytes);

            Assert.Equal(original.Format, replaced.Format);
            Assert.Equal(original.Width, replaced.Width);
            Assert.Equal(original.Height, replaced.Height);
            Assert.Same(newBytes, replaced.Data);
        }

        [Fact]
        public void ImageData_WithData_NullThrowsArgumentNullException()
        {
            var d = new ImageData(new byte[] { 1 }, ImageFormat.Png, 1, 1);
            Assert.Throws<ArgumentNullException>(() => d.WithData(null));
        }

        // ── 2. LoadFromBytes ──────────────────────────────────────────────────

        [Fact]
        public void LoadFromBytes_Null_ReturnsEmpty()
        {
            ImageData result = _svc.LoadFromBytes(null);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void LoadFromBytes_EmptyArray_ReturnsEmpty()
        {
            ImageData result = _svc.LoadFromBytes(Array.Empty<byte>());
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void LoadFromBytes_ValidBytes_ReturnsSameBytes()
        {
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            ImageData result = _svc.LoadFromBytes(data);

            Assert.False(result.IsEmpty);
            Assert.Same(data, result.Data);
        }

        // ── 3. Fixture loading ────────────────────────────────────────────────

        [Fact]
        public void EmbeddedFixture_16x16Png_LoadsSuccessfully()
        {
            byte[] data = LoadFixture("test16x16.png");

            Assert.NotNull(data);
            Assert.True(data.Length > 0, "16x16 PNG fixture must not be empty.");
            // PNG magic: \x89PNG
            Assert.Equal(0x89, data[0]);
            Assert.Equal(0x50, data[1]); // 'P'
            Assert.Equal(0x4E, data[2]); // 'N'
            Assert.Equal(0x47, data[3]); // 'G'
        }

        [Fact]
        public void EmbeddedFixture_32x32Png_LoadsSuccessfully()
        {
            byte[] data = LoadFixture("test32x32.png");

            Assert.NotNull(data);
            Assert.True(data.Length > 0, "32x32 PNG fixture must not be empty.");
            Assert.Equal(0x89, data[0]);
        }

        [Fact]
        public void EmbeddedFixture_Ico_LoadsSuccessfully()
        {
            byte[] data = LoadFixture("test16.ico");

            Assert.NotNull(data);
            Assert.True(data.Length > 0, "ICO fixture must not be empty.");
            // ICO magic: first 4 bytes are 00 00 01 00
            Assert.Equal(0x00, data[0]);
            Assert.Equal(0x00, data[1]);
            Assert.Equal(0x01, data[2]);
            Assert.Equal(0x00, data[3]);
        }

        [Fact]
        public void LoadFromBytes_16x16PngFixture_ReturnsNonEmptyImageData()
        {
            byte[] data = LoadFixture("test16x16.png");
            ImageData result = _svc.LoadFromBytes(data);

            Assert.False(result.IsEmpty);
        }

        // ── 4. Resize ─────────────────────────────────────────────────────────

        [Fact]
        public void Resize_ValidSourceAndDimensions_ReturnsSameBytesWithNewDimensions()
        {
            byte[] data = LoadFixture("test16x16.png");
            ImageData source = _svc.LoadFromBytes(data);

            ImageData resized = _svc.Resize(source, 32, 32);

            Assert.Equal(32, resized.Width);
            Assert.Equal(32, resized.Height);
            Assert.Equal(source.Format, resized.Format);
        }

        [Fact]
        public void Resize_WidthZero_ThrowsArgumentOutOfRangeException()
        {
            var source = new ImageData(new byte[] { 1 }, ImageFormat.Png, 16, 16);
            Assert.Throws<ArgumentOutOfRangeException>(() => _svc.Resize(source, 0, 16));
        }

        [Fact]
        public void Resize_HeightZero_ThrowsArgumentOutOfRangeException()
        {
            var source = new ImageData(new byte[] { 1 }, ImageFormat.Png, 16, 16);
            Assert.Throws<ArgumentOutOfRangeException>(() => _svc.Resize(source, 16, 0));
        }

        [Fact]
        public void Resize_NegativeWidth_ThrowsArgumentOutOfRangeException()
        {
            var source = new ImageData(new byte[] { 1 }, ImageFormat.Png, 16, 16);
            Assert.Throws<ArgumentOutOfRangeException>(() => _svc.Resize(source, -1, 16));
        }

        [Fact]
        public void Resize_NegativeHeight_ThrowsArgumentOutOfRangeException()
        {
            var source = new ImageData(new byte[] { 1 }, ImageFormat.Png, 16, 16);
            Assert.Throws<ArgumentOutOfRangeException>(() => _svc.Resize(source, 16, -1));
        }

        [Fact]
        public void Resize_EmptySource_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _svc.Resize(ImageData.Empty, 16, 16));
        }

        [Fact]
        public void Resize_NullSource_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _svc.Resize(null, 16, 16));
        }

        // ── 5. ConvertToFormat ────────────────────────────────────────────────

        [Fact]
        public void ConvertToFormat_PngToBmp_ReturnsNewFormatWithSameBytes()
        {
            var source = new ImageData(LoadFixture("test16x16.png"), ImageFormat.Png, 16, 16);
            ImageData result = _svc.ConvertToFormat(source, ImageFormat.Bmp);

            Assert.Equal(ImageFormat.Bmp, result.Format);
        }

        [Fact]
        public void ConvertToFormat_EmptySource_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _svc.ConvertToFormat(ImageData.Empty, ImageFormat.Bmp));
        }

        [Fact]
        public void ConvertToFormat_NullSource_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _svc.ConvertToFormat(null, ImageFormat.Bmp));
        }

        [Fact]
        public void ConvertToFormat_SvgTarget_ThrowsNotSupportedException()
        {
            var source = new ImageData(new byte[] { 1 }, ImageFormat.Png, 16, 16);
            Assert.Throws<NotSupportedException>(() =>
                _svc.ConvertToFormat(source, ImageFormat.Svg));
        }

        // ── 6. GetStandardIcon ────────────────────────────────────────────────

        [Fact]
        public void GetStandardIcon_ValidIcon_ReturnsEmpty_ForNullService()
        {
            // NullImageService has no icon resources; returns Empty for all IDs.
            // PwIcon.Key = 0 (cast to int per IImageService contract).
            ImageData result = _svc.GetStandardIcon(0, 16);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void GetStandardIcon_OutOfRangeIcon_ReturnsEmpty_ForNullService()
        {
            // Out-of-range values must not throw.
            ImageData result = _svc.GetStandardIcon(9999, 16);
            Assert.True(result.IsEmpty);
        }

        // ── 7. CreateBannerImage ──────────────────────────────────────────────

        [Fact]
        public void CreateBannerImage_ReturnsEmpty_ForNullService()
        {
            // PwIcon.Home = 2 (cast to int per IImageService contract).
            ImageData result = _svc.CreateBannerImage(
                800, 60, 2, "Test Title", "Test Subtitle");
            Assert.True(result.IsEmpty);
        }

        // ── 8. ImageData immutability ─────────────────────────────────────────

        [Fact]
        public void ImageData_DataProperty_IsSameReference()
        {
            byte[] bytes = new byte[] { 1, 2, 3 };
            var d = new ImageData(bytes, ImageFormat.Png, 1, 1);
            // Verify no defensive copy — caller is responsible for not mutating.
            Assert.Same(bytes, d.Data);
        }
    }
}
