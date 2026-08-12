using System;

namespace KeePass.Core.Services
{
    /// <summary>
    /// No-op implementation of <see cref="IImageService"/> for test scenarios
    /// and environments where image rendering is not available.
    ///
    /// All operations that require platform-specific image decoding return
    /// <see cref="ImageData.Empty"/> instead of throwing.
    /// Operations that validate parameters (Resize, ConvertToFormat) still
    /// throw the documented exceptions so contract tests can verify them.
    /// </summary>
    public sealed class NullImageService : IImageService
    {
        /// <summary>Singleton instance for convenient use in tests.</summary>
        public static readonly NullImageService Instance = new NullImageService();

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <see cref="ImageData.Empty"/> for null or empty input.
        /// For non-empty input, returns an <see cref="ImageData"/> with the
        /// supplied bytes, <see cref="ImageFormat.Png"/>, and 0×0 dimensions
        /// (dimensions cannot be decoded without a real image library).
        /// </remarks>
        public ImageData LoadFromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
                return ImageData.Empty;

            return new ImageData(data, ImageFormat.Png, 0, 0);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="width"/> or <paramref name="height"/>
        /// is &lt;= 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="source"/> is empty.
        /// </exception>
        public ImageData Resize(ImageData source, int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException("width", "Width must be > 0.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException("height", "Height must be > 0.");
            if (source == null || source.IsEmpty)
                throw new ArgumentException("Source image data must not be empty.", "source");

            return new ImageData(source.Data, source.Format, width, height);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="source"/> is empty.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when <paramref name="targetFormat"/> is
        /// <see cref="ImageFormat.Svg"/>, which cannot be produced by
        /// re-encoding raster data.
        /// </exception>
        public ImageData ConvertToFormat(ImageData source, ImageFormat targetFormat)
        {
            if (source == null || source.IsEmpty)
                throw new ArgumentException("Source image data must not be empty.", "source");
            if (targetFormat == ImageFormat.Svg)
                throw new NotSupportedException(
                    "Converting raster images to SVG is not supported.");

            return new ImageData(source.Data, targetFormat, source.Width, source.Height);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <see cref="ImageData.Empty"/> regardless of
        /// <paramref name="iconId"/>; NullImageService has no icon resources.
        /// </remarks>
        public ImageData GetStandardIcon(int iconId, int size)
        {
            return ImageData.Empty;
        }

        /// <inheritdoc/>
        /// <remarks>Returns <see cref="ImageData.Empty"/>.</remarks>
        public ImageData CreateBannerImage(int width, int height, int iconId,
            string title, string subtitle)
        {
            return ImageData.Empty;
        }
    }
}
