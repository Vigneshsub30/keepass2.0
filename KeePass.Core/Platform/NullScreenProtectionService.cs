namespace KeePass.Core.Platform
{
    /// <summary>
    /// No-op implementation of <see cref="IScreenProtectionService"/> used on
    /// platforms that do not support screen-capture protection (Linux in v1).
    ///
    /// <see cref="IsSupported"/> is always <c>false</c>.
    /// <see cref="Enable"/> and <see cref="Disable"/> are safe no-ops.
    /// </summary>
    public sealed class NullScreenProtectionService : IScreenProtectionService
    {
        /// <inheritdoc/>
        public bool IsSupported => false;

        /// <inheritdoc/>
        public void Enable() { }

        /// <inheritdoc/>
        public void Disable() { }
    }
}
