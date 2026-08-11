using System;

namespace KeePass.Core.Services
{
    /// <summary>
    /// Immutable value type that carries encoded image bytes together with
    /// format and dimension metadata.  Used as the platform-neutral replacement
    /// for <c>System.Drawing.Image</c> in cross-platform code paths.
    ///
    /// Thread-safe: the byte array is never mutated after construction.
    /// </summary>
    public sealed class ImageData
    {
        /// <summary>
        /// Returns an empty / null image with zero dimensions and
        /// <see cref="ImageFormat.Png"/> as a default format.  Returned by
        /// <see cref="IImageService"/> implementations when the input is
        /// invalid or unavailable.
        /// </summary>
        public static readonly ImageData Empty = new ImageData(
            Array.Empty<byte>(), ImageFormat.Png, 0, 0);

        /// <summary>Raw encoded bytes in <see cref="Format"/>.</summary>
        public byte[] Data { get; }

        /// <summary>Encoding format of <see cref="Data"/>.</summary>
        public ImageFormat Format { get; }

        /// <summary>Width in pixels, or 0 for <see cref="Empty"/>.</summary>
        public int Width { get; }

        /// <summary>Height in pixels, or 0 for <see cref="Empty"/>.</summary>
        public int Height { get; }

        /// <summary>
        /// <c>true</c> if this instance carries no image data
        /// (i.e. <see cref="Data"/> is empty).
        /// </summary>
        public bool IsEmpty => Data == null || Data.Length == 0;

        /// <summary>
        /// Initialises a new <see cref="ImageData"/>.
        /// </summary>
        /// <param name="data">
        /// Encoded image bytes.  Must not be null; pass
        /// <see cref="Array.Empty{T}"/> to represent the absence of an image.
        /// </param>
        /// <param name="format">Encoding format of <paramref name="data"/>.</param>
        /// <param name="width">Width in pixels (0 for empty images).</param>
        /// <param name="height">Height in pixels (0 for empty images).</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="data"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="width"/> or <paramref name="height"/>
        /// is negative.
        /// </exception>
        public ImageData(byte[] data, ImageFormat format, int width, int height)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (width < 0) throw new ArgumentOutOfRangeException("width", "Width must be >= 0.");
            if (height < 0) throw new ArgumentOutOfRangeException("height", "Height must be >= 0.");

            Data = data;
            Format = format;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Returns a copy with the same format and dimensions but substituting
        /// <paramref name="newData"/> as the encoded bytes.  Useful for format
        /// conversion results that share metadata.
        /// </summary>
        public ImageData WithData(byte[] newData)
        {
            if (newData == null) throw new ArgumentNullException("newData");
            return new ImageData(newData, Format, Width, Height);
        }
    }
}
