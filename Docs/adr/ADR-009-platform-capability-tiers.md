# ADR-009: Platform Capability-Tier Matrix Definition

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

Platform capabilities in KeePass 2.x are determined by a collection of
scattered ad-hoc checks:

- `NativeLib.IsUnix()` — true on macOS and Linux
- `NativeLib.GetPlatformID()` — returns `Unix`, `MacOSX`, or `Windows`
- `NativeLib.GetDesktopType()` — returns Unity, KDE, GNOME, Cinnamon,
  Pantheon, MATE, LXDE, XFCE, or Windows
- `NativeLib.IsWayland()` — Wayland vs X11 session detection
- `NativeLib.ProcessArchitecture` — X86, X64, Arm, Arm64
- `MonoWorkarounds.IsRequired(bugId)` — Mono version-gated workarounds

There is no centralized capability model. Feature availability is expressed
as `if (NativeLib.IsUnix()) { … }` blocks scattered across 45+ call sites
in `UIUtil.cs`, `ClipboardUtil.cs`, `FileTransactionEx.cs`, `HotKeyManager.cs`,
and dozens of other files.

### Feature-Parity Matrix

| Capability | Windows | macOS | Linux |
|-----------|---------|-------|-------|
| **Credential delivery: clipboard** | ✅ P/Invoke, 15 retries | ✅ pbcopy/pbpaste | ✅ xsel (must be installed) |
| **Clipboard history suppression** | ✅ AttachIgnoreFormatsW | ❌ Not available | ❌ Not available |
| **Credential delivery: auto-type** | ✅ SendInput (SiEngineWin) | ❌ v1 not in scope | ❌ v1 not in scope |
| **Credential delivery: browser ext.** | ❌ Not in scope | ❌ Not in scope | ❌ Not in scope |
| **Screen-capture protection** | ✅ `SetWindowDisplayAffinity` | ❌ Not available | ❌ Not available |
| **Secure desktop (key entry)** | ✅ ProtectedDialog | ❌ Not available | ❌ Not available |
| **Global hot keys** | ✅ `RegisterHotKey` (5 keys) | ⚠️ Partial (OS intercepts some) | ❌ Requires X11 tools |
| **Taskbar integration** | ✅ RegisterWindowMessage, DWM | ❌ Dock API only | ❌ DE-specific |
| **Atomic saves: TxF** | ✅ NTFS only | ❌ Not available | ❌ Not available |
| **Atomic saves: two-phase rename** | ✅ MoveFileEx | ✅ rename(2) | ✅ rename(2) |
| **Native crypto: AES-KDF (fast)** | ✅ CNG (Aes.Create) | ✅ OpenSSL | ✅ OpenSSL / LibGCrypt |
| **Native crypto: Argon2** | ✅ KeePassLibN (x64/ARM) | ⚠️ Managed fallback | ⚠️ Managed / LibArgon2 |
| **Credential store** | ✅ DPAPI (Windows Credential Store) | ⚠️ Keychain (planned) | ⚠️ libsecret (planned) |
| **File system ACLs** | ✅ NTFS FileSecurity | ❌ POSIX mode bits only | ❌ POSIX mode bits only |
| **EFS encryption** | ✅ Windows NTFS only | ❌ Not available | ❌ Not available |
| **Vista task dialogs** | ✅ TaskDialogIndirect | ❌ Not available | ❌ Not available |
| **DWM effects (blur etc.)** | ✅ DwmEnableBlurBehindWindow | ❌ Not available | ❌ Not available |
| **KDE Klipper clipboard clear** | N/A | N/A | ✅ 6×125ms loop |
| **Wayland clipboard** | N/A | N/A | ❌ v1 X11 only (xsel) |

**Legend:**
- ✅ Available
- ⚠️ Partial or degraded
- ❌ Unavailable in v1

### Platform Detection Code

| Method | Returns | Usage |
|--------|---------|-------|
| `NativeLib.IsUnix()` | `true` on macOS + Linux | Broad platform branch |
| `NativeLib.GetPlatformID()` | `MacOSX`, `Unix`, `Windows` | Fine-grained OS detection |
| `NativeLib.GetDesktopType()` | `KDE`, `GNOME`, `Cinnamon`, `Windows`, etc. | Clipboard / window manager quirks |
| `NativeLib.IsWayland()` | `true` when `WAYLAND_DISPLAY` is set | Wayland-specific paths |
| `NativeLib.ProcessArchitecture` | `X86`, `X64`, `Arm`, `Arm64` | Native library selection |

## Decision

We define three explicit capability tiers for the .NET 10 v1 release:

### Tier A — Full (Windows)

All features are available on Windows 10 / 11 x64 and ARM64:

- **Clipboard**: P/Invoke with `AttachIgnoreFormatsW` (history suppression)
- **Auto-type**: `SendInput` via `SiEngineWin.cs`
- **Screen-capture protection**: `SetWindowDisplayAffinity`
- **Secure desktop**: `ProtectedDialog`
- **Global hot keys**: `RegisterHotKey`
- **Taskbar**: `RegisterWindowMessage`, DWM blur
- **Atomic saves**: TxF (preferred) + two-phase `MoveFileEx` fallback
- **Native crypto**: CNG (AES), KeePassLibN (Argon2 x64/ARM64)
- **Credential store**: DPAPI
- **File ACLs**: NTFS FileSecurity (full read/write)
- **EFS**: `File.Encrypt()` / `File.Decrypt()`
- **Vista task dialogs**: `TaskDialogIndirect`

### Tier B — Standard (macOS)

Available on macOS 13 (Ventura) and later, x64 and ARM64 (Apple Silicon):

- **Clipboard**: `pbcopy` / `pbpaste`
- **Auto-type**: ❌ Not in v1 scope
- **Screen-capture protection**: ❌ Not available
- **Secure desktop**: ❌ Not available
- **Global hot keys**: ⚠️ Partial — macOS intercepts some key combinations
- **Taskbar**: ❌ Dock API only (no DWM)
- **Atomic saves**: POSIX `rename(2)`
- **Native crypto**: OpenSSL (AES), managed Argon2 fallback
- **Credential store**: ⚠️ macOS Keychain — planned, not in v1
- **File ACLs**: ❌ POSIX mode bits only

### Tier C — Standard (Linux)

Available on Ubuntu 22.04 LTS, Fedora 38+, and compatible distributions.
Requires X11 session (Wayland not supported in v1):

- **Clipboard**: `xsel` (must be installed separately)
- **Auto-type**: ❌ Not in v1 scope
- **Screen-capture protection**: ❌ Not available
- **Secure desktop**: ❌ Not available
- **Global hot keys**: ❌ Requires X11 tools
- **Taskbar**: ❌ Desktop-environment specific
- **Atomic saves**: POSIX `rename(2)`
- **Native crypto**: OpenSSL (AES), LibArgon2 (when available)
- **Credential store**: ⚠️ libsecret — planned, not in v1
- **File ACLs**: ❌ POSIX mode bits only
- **KDE Klipper**: ✅ 6×125ms clipboard clear loop (handled in `LinuxClipboardService`)

### IPlatformIntegration Interface Contract

The `IPlatformIntegration` interface (`KeePass.Platform.Unix/IPlatformIntegration.cs`
and platform implementations) replaces ad-hoc `NativeLib.IsUnix()` checks with
structured capability detection:

```csharp
public interface IPlatformIntegration
{
    // Clipboard
    bool SupportsClipboardHistorySuppression { get; }  // Windows only
    bool RequiresKdeKlipperWorkaround { get; }          // Linux KDE only

    // Window management
    bool SupportsAlwaysOnTop { get; }                   // Not Cinnamon
    bool RequiresWindowMinSizeEnforcement { get; }      // Linux

    // URL / process launching
    bool OpenUrl(string url);                           // xdg-open / open
    bool StartProcess(ProcessStartInfo psi);            // platform-specific

    // Crypto
    bool SupportsNativeArgon2 { get; }                  // LibArgon2 probe
}
```

Sub-interfaces are defined for each capability domain and registered in the
DI container at startup. `WindowsPlatformIntegration`, `LinuxPlatformIntegration`,
and `MacOsPlatformIntegration` implement platform-specific behaviors.

### v1 Credential Delivery Baseline

**Clipboard-with-auto-clear is the confirmed v1 credential delivery mechanism
for macOS (Tier B) and Linux (Tier C).** Auto-type and browser extension
support are planned for future releases and are not in scope for the initial
.NET 10 port. This aligns with the decision documented in
[ADR-007](ADR-007-clipboard-credential-delivery.md).

## Consequences

### Positive

- **Explicit contract**: the capability matrix is the single source of truth.
  Contributors can look up any feature and know its tier-specific availability
  without tracing platform-detection code.
- **Structured capability detection**: `IPlatformIntegration` replaces 45+
  `NativeLib.IsUnix()` call sites with typed properties. New platform features
  add a property to the interface and a concrete implementation, not another
  scattered `if` branch.
- **User-facing clarity**: the tier documentation informs release notes and
  user-facing feature disclosures.

### Negative

- **Macros/auto-type gap on macOS and Linux**: users who rely on auto-type
  as their primary credential delivery method on macOS or Linux will need to
  use clipboard instead, accepting the weaker security model.
- **xsel dependency on Linux**: clipboard functionality on Linux requires
  `xsel` to be installed. On minimal server distributions it may not be
  present. There is no graceful degradation in v1.
- **Wayland not supported**: users on a pure Wayland session (no XWayland)
  cannot use clipboard in v1.

### Neutral

- Tier B (macOS) and Tier C (Linux) share most capability gaps. They are
  defined as separate tiers because desktop-environment detection (KDE, GNOME,
  Cinnamon) and clipboard tool differences (`pbcopy` vs `xsel`) require
  separate concrete implementations.
- The capability matrix will require updates as new platform features are
  implemented in future releases (e.g., Keychain credential store, Wayland
  clipboard via `wl-copy`).

## Edge Cases

1. **Wayland vs X11 on Linux**: `NativeLib.IsWayland()` returns `true` when
   `WAYLAND_DISPLAY` is set. In v1, `xsel` requires X11. A Wayland session
   with XWayland installed (`DISPLAY` also set) can use `xsel` through the
   XWayland bridge. A pure Wayland session without XWayland cannot. The
   capability tier documents this as "❌ v1 X11 only".

2. **Desktop environment detection on Linux**: `NativeLib.GetDesktopType()`
   reads `XDG_CURRENT_DESKTOP` and `XDG_SESSION_DESKTOP`. If neither is set
   (headless or minimal desktop), the detection returns `Windows` on Windows
   and the default (no special workarounds) on Linux. `LinuxPlatformIntegration`
   returns `SupportsAlwaysOnTop = true` when the desktop type is unknown
   (conservative assumption — Cinnamon disables it, but unknown desktops
   likely support it).

3. **ARM64 and native crypto**: `KeePassLibN` (the native Argon2 library)
   ships with x64, x86, and ARM64 Windows DLLs. On macOS ARM64 (Apple
   Silicon) and Linux ARM64, the managed Argon2 implementation is used as a
   fallback. The managed implementation is functionally correct but slower
   than the native library by approximately 5×.

4. **Mono returning `Unix` for macOS**: historical Mono runtimes returned
   `PlatformID.Unix` for macOS. `NativeLib.GetPlatformID()` uses a secondary
   check (`Environment.OSVersion.Platform == PlatformID.MacOSX` or `uname -s`
   returning `Darwin`) to distinguish macOS from Linux on Mono. On .NET 10,
   `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)` is definitive.

## References

- `KeePass/Native/NativeLib.cs` — `IsUnix()`, `GetPlatformID()`,
  `GetDesktopType()`, `IsWayland()`, `ProcessArchitecture`
- `KeePass.Platform.Unix/LinuxPlatformIntegration.cs` — Tier C implementation
- `KeePass.Platform.Unix/MacOsPlatformIntegration.cs` — Tier B implementation
- `KeePass/Util/ClipboardUtil.Windows.cs` — Tier A clipboard (P/Invoke)
- `KeePass/Util/ClipboardUtil.Unix.cs` — Tier B/C clipboard (pbcopy / xsel)
- [ADR-005](ADR-005-mono-workaround-retirement.md) — Mono workaround
  retirement (platform capability origins)
- [ADR-007](ADR-007-clipboard-credential-delivery.md) — Clipboard credential
  delivery mechanism
- [ADR-008](ADR-008-atomic-save-guarantees.md) — Atomic-save transaction
  guarantees
- [ADR-000](ADR-000-template.md) — ADR template
