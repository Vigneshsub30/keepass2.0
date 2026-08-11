using System;
using KeePass.Core.Platform;
using Xunit;

namespace KeePass.Tests.Platform.Unix
{
    /// <summary>
    /// Characterization tests for <see cref="NullAutoTypeService"/> and
    /// <see cref="NullScreenProtectionService"/>.  Both are thin stubs and run
    /// on every platform without native dependencies.
    /// </summary>
    public class NullServiceTests
    {
        // ── NullAutoTypeService ───────────────────────────────────────────

        [Fact]
        public void NullAutoTypeService_IsSupported_ReturnsFalse()
        {
            var svc = new NullAutoTypeService();
            Assert.False(svc.IsSupported);
        }

        [Fact]
        public void NullAutoTypeService_PerformAutoType_NullCtx_ThrowsArgumentNull()
        {
            var svc = new NullAutoTypeService();
            Assert.Throws<ArgumentNullException>(() => svc.PerformAutoType(null));
        }

        [Fact]
        public void NullAutoTypeService_PerformAutoType_ValidCtx_ThrowsPlatformNotSupported()
        {
            var svc = new NullAutoTypeService();
            var ctx = new AutoTypeContext("{USERNAME}", null);
            Assert.Throws<PlatformNotSupportedException>(() => svc.PerformAutoType(ctx));
        }

        [Fact]
        public void NullAutoTypeService_ImplementsInterface()
        {
            IAutoTypeService svc = new NullAutoTypeService();
            Assert.NotNull(svc);
        }

        // ── NullScreenProtectionService ───────────────────────────────────

        [Fact]
        public void NullScreenProtectionService_IsSupported_ReturnsFalse()
        {
            var svc = new NullScreenProtectionService();
            Assert.False(svc.IsSupported);
        }

        [Fact]
        public void NullScreenProtectionService_Enable_DoesNotThrow()
        {
            var svc = new NullScreenProtectionService();
            svc.Enable(); // must be a safe no-op
        }

        [Fact]
        public void NullScreenProtectionService_Disable_DoesNotThrow()
        {
            var svc = new NullScreenProtectionService();
            svc.Disable(); // must be a safe no-op
        }

        [Fact]
        public void NullScreenProtectionService_ImplementsInterface()
        {
            IScreenProtectionService svc = new NullScreenProtectionService();
            Assert.NotNull(svc);
        }
    }
}
