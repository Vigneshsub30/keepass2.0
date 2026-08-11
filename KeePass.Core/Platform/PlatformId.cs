namespace KeePass.Core.Platform
{
    /// <summary>
    /// Identifies the host operating system for platform-capability routing.
    /// Determined at application startup by the platform bootstrap and injected
    /// into <see cref="IPlatformIntegration"/>.
    /// </summary>
    public enum PlatformId
    {
        /// <summary>Windows (any edition where .NET is supported).</summary>
        Windows,

        /// <summary>macOS (Darwin).</summary>
        MacOS,

        /// <summary>Linux (any distribution where .NET is supported).</summary>
        Linux
    }
}
