using System;

namespace KeePass.Core.Platform
{
    /// <summary>
    /// No-op implementation of <see cref="IAutoTypeService"/> used on platforms
    /// where keyboard auto-type injection is not available (macOS, Linux in v1).
    ///
    /// <see cref="IsSupported"/> is always <c>false</c>.
    /// Calling <see cref="PerformAutoType"/> throws
    /// <see cref="PlatformNotSupportedException"/>.
    /// </summary>
    public sealed class NullAutoTypeService : IAutoTypeService
    {
        /// <inheritdoc/>
        public bool IsSupported => false;

        /// <inheritdoc/>
        /// <exception cref="PlatformNotSupportedException">Always thrown.</exception>
        public void PerformAutoType(AutoTypeContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException("ctx");
            throw new PlatformNotSupportedException(
                "Auto-type keyboard injection is not supported on this platform.");
        }
    }
}
