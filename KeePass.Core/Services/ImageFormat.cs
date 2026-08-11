namespace KeePass.Core.Services
{
    /// <summary>
    /// Identifies the encoded format of image bytes stored in an
    /// <see cref="ImageData"/> value.
    /// </summary>
    public enum ImageFormat
    {
        /// <summary>Portable Network Graphics (.png).</summary>
        Png = 0,

        /// <summary>Windows icon format, possibly multi-resolution (.ico).</summary>
        Ico = 1,

        /// <summary>Windows bitmap (.bmp).</summary>
        Bmp = 2,

        /// <summary>Joint Photographic Experts Group (.jpg / .jpeg).</summary>
        Jpeg = 3,

        /// <summary>Scalable Vector Graphics (.svg).</summary>
        Svg = 4,
    }
}
