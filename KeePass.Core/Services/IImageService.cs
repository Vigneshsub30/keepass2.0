using System;

namespace KeePass.Core.Services
{
    /// <summary>
    /// Platform-neutral abstraction for image loading, resizing, format
    /// conversion, and standard icon retrieval.
    ///
    /// Platform-specific implementations (WinForms/System.Drawing,
    /// Avalonia/SkiaSharp) are registered at application start-up and must
    /// not be referenced by cross-platform assemblies (KeePassLib, KeePass.Core).
    ///
    /// All methods are documented to be safe to call from any thread.
    /// Implementations that require a UI thread must marshal internally.
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Decodes <paramref name="data"/> and returns an <see cref="ImageData"/>
        /// carrying the decoded dimensions and the original bytes.
        ///
        /// Returns <see cref="ImageData.Empty"/> when <paramref name="data"/> is
        /// null, empty, or cannot be decoded — never throws for bad input.
        /// </summary>
        /// <param name="data">
        /// Raw bytes of a supported image format (PNG, ICO, BMP, JPEG, SVG).
        /// </param>
        ImageData LoadFromBytes(byte[] data);

        /// <summary>
        /// Returns a new <see cref="ImageData"/> scaled to
        /// <paramref name="width"/> × <paramref name="height"/> pixels.
        /// The source format is preserved in the returned value.
        /// </summary>
        /// <param name="source">Source image data.  Must not be empty.</param>
        /// <param name="width">Target width in pixels.  Must be &gt; 0.</param>
        /// <param name="height">Target height in pixels.  Must be &gt; 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="width"/> or <paramref name="height"/>
        /// is &lt;= 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="source"/> is empty or corrupt.
        /// </exception>
        ImageData Resize(ImageData source, int width, int height);

        /// <summary>
        /// Re-encodes <paramref name="source"/> into <paramref name="targetFormat"/>
        /// and returns the result.
        /// </summary>
        /// <param name="source">Source image data.  Must not be empty.</param>
        /// <param name="targetFormat">
        /// Desired output format.  Must be a value the implementation supports
        /// for encoding (typically PNG, BMP, JPEG).
        /// </param>
        /// <exception cref="NotSupportedException">
        /// Thrown when the implementation cannot encode to
        /// <paramref name="targetFormat"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="source"/> is empty.
        /// </exception>
        ImageData ConvertToFormat(ImageData source, ImageFormat targetFormat);

        /// <summary>
        /// Returns the standard icon for <paramref name="iconId"/> at the
        /// requested pixel <paramref name="size"/>.
        ///
        /// Returns a default placeholder icon when <paramref name="iconId"/>
        /// is out of range rather than throwing.
        /// </summary>
        /// <param name="iconId">
        /// Standard KeePass icon identifier as an <c>int</c>.  Cast from
        /// <c>KeePassLib.PwIcon</c> at the call site.  Using <c>int</c>
        /// here keeps <c>KeePass.Core</c> free of the <c>KeePassLib</c>
        /// project reference; <c>KeePassLib</c> currently targets
        /// <c>net10.0-windows</c> and is incompatible with the
        /// cross-platform <c>net10.0</c> TFM of this library (WO-034 will
        /// decouple <c>KeePassLib</c> from WinForms, at which point the
        /// parameter type can be changed to <c>PwIcon</c>).
        /// </param>
        /// <param name="size">
        /// Requested icon size in pixels (e.g. 16 or 32).
        /// Must be &gt; 0.
        /// </param>
        ImageData GetStandardIcon(int iconId, int size);

        /// <summary>
        /// Creates a banner image as used in the KeePass dialog chrome.
        /// Returns <see cref="ImageData.Empty"/> if the implementation cannot
        /// render banners (e.g. <see cref="NullImageService"/>).
        /// </summary>
        /// <param name="width">Banner width in pixels.  Must be &gt; 0.</param>
        /// <param name="height">Banner height in pixels.  Must be &gt; 0.</param>
        /// <param name="iconId">
        /// Icon to embed in the banner, as an <c>int</c> cast from
        /// <c>KeePassLib.PwIcon</c>.  See <see cref="GetStandardIcon"/> for
        /// rationale.
        /// </param>
        /// <param name="title">Title text rendered on the banner.</param>
        /// <param name="subtitle">Subtitle text rendered on the banner.</param>
        ImageData CreateBannerImage(int width, int height, int iconId,
            string title, string subtitle);
    }
}
