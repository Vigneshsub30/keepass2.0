using System;

using KeePass.Core.Platform;
using KeePass.Platform;

using Xunit;

namespace KeePass.Tests.Platform.Windows
{
    /// <summary>
    /// Characterization tests for <see cref="WindowsAutoTypeService"/>.
    ///
    /// Tests in this file verify the contract behavior:
    ///   - <see cref="IAutoTypeService.IsSupported"/> is always <c>true</c> on Windows.
    ///   - <see cref="IAutoTypeService.PerformAutoType"/> throws
    ///     <see cref="ArgumentNullException"/> for null context.
    ///   - The service can be assigned to <see cref="IAutoTypeService"/>.
    ///
    /// Live auto-type tests (actually sending keystrokes) require a desktop
    /// session and a target window; they are omitted from the automated suite.
    /// </summary>
    public class WindowsAutoTypeServiceTests
    {
        private readonly IAutoTypeService _svc = new WindowsAutoTypeService();

        // ── 1. Contract: IsSupported ───────────────────────────────────────

        [Fact]
        public void IsSupported_IsTrue_OnWindows()
        {
            Assert.True(_svc.IsSupported);
        }

        [Fact]
        public void CanBeAssignedToInterface()
        {
            IAutoTypeService asInterface = new WindowsAutoTypeService();
            Assert.NotNull(asInterface);
        }

        // ── 2. Parameter validation: PerformAutoType ──────────────────────

        [Fact]
        public void PerformAutoType_NullContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.PerformAutoType(null));
        }
    }
}
