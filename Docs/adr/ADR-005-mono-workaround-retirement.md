# ADR-005: Mono Workaround Retirement Strategy

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

`KeePassLib/Utility/MonoWorkarounds.cs` (now deleted) contained 45 numbered
workarounds for documented defects in the Mono runtime. Each workaround was
gated by `MonoWorkarounds.IsRequired(bugId)`, which compared the current Mono
version string against a minimum version for each bug.

On .NET 10, Mono is no longer the runtime. Most workarounds became dead code.
However, some workarounds encoded real platform behaviors that must be preserved
on Linux and macOS regardless of the runtime:

| Classification | Count | Meaning |
|---|---|---|
| **RETIRE** | 36 | Mono-only defect; .NET 10 does not exhibit this behavior |
| **RE-IMPLEMENT** | 5 | Real platform behavior still needed; migrate to `IPlatformIntegration` |
| **OBSOLETE** | 4 | Feature removed or workaround was already hard-coded to `false` |
| **Total** | **45** | |

The complete inventory with per-workaround trigger conditions and migration
targets is in
[MonoWorkarounds-Classification.md](MonoWorkarounds-Classification.md).

### Key RE-IMPLEMENT Workarounds

| Bug ID | Behavior | Migration Target |
|--------|----------|-----------------|
| 1716 | `AlwaysOnTop` ignored on Cinnamon desktop | `IPlatformIntegration.SupportsAlwaysOnTop` |
| 686017 | Window minimum sizes not enforced by WM | `IPlatformIntegration.RequiresWindowMinSizeEnforcement` |
| 19836 | `Process.Start` does not open URLs on Linux/macOS | `IPlatformIntegration.OpenUrl(string)` |
| 190417 / 3471228285 | CLI argument encoding differs between platforms | `IPlatformIntegration.StartProcess(ProcessStartInfo)` |
| 100004 | Native Argon2 for key-derivation performance | `IPlatformIntegration.GetNativeArgon2` capability probe |

### Key RETIRE Workarounds (Security-Relevant)

| Bug ID | Behavior | Why Retire |
|--------|----------|-----------|
| 9604 | Resolving a non-existing metadata token crashes Mono | .NET 10 throws a typed exception; crash workaround (skip reference-token check on Unix) is removed |
| 10163 | `WebRequest.GetResponse` missing; breaks WebDAV PUT | .NET 10 `HttpClient` fully implements PUT |

**The most security-relevant retirement** is bug #9604. Its workaround skipped
the entire reference-compatibility check in `PluginManager.CheckCompatibility`
on non-Windows platforms via `NativeLib.IsUnix()`. This created a cross-platform
asymmetry where a plugin rejected on Windows was silently admitted on macOS and
Linux. On .NET 10, `Module.ResolveType` and `Module.ResolveMember` work
correctly on all platforms, so the Unix skip has been removed.

### `Thread.Abort()` Removal

`MonoWorkarounds.Terminate()` called `g_thFixClip.Abort()` to cancel the
clipboard-fix background thread. `Thread.Abort()` is unsupported on .NET 5+.
Since workaround #1530 (the clipboard thread) is OBSOLETE and the thread is
never started on .NET 10, `g_thFixClip` and `Terminate()` were deleted entirely
in WO-035. Future background threads must use `CancellationToken`.

### Platform Detection Before Retirement

`MonoWorkarounds.IsRequired()` checked the runtime version string. Call sites
also used:

- `NativeLib.IsUnix()` — true on macOS and Linux
- `NativeLib.GetPlatformID()` — returns the OS identifier
- `NativeLib.GetDesktopType()` — detects Unity, KDE, GNOME, Cinnamon
- `NativeLib.IsWayland()` — Wayland vs X11 session

These detection methods remain valid on .NET 10 and are used by
`IPlatformIntegration` implementations.

## Decision

We retire `MonoWorkarounds.cs` completely and replace the workaround pattern
with explicit capability detection via `IPlatformIntegration`:

### `IPlatformIntegration` Interface Design

```csharp
// KeePass.Core/Platform/IPlatformIntegration.cs
public interface IPlatformIntegration
{
    // Window management
    bool SupportsAlwaysOnTop { get; }              // #1716
    bool RequiresWindowMinSizeEnforcement { get; } // #686017

    // Process/URL launching
    bool OpenUrl(string url);                       // #19836
    bool StartProcess(ProcessStartInfo psi);        // #190417, #3471228285

    // Native crypto
    bool SupportsNativeArgon2 { get; }              // #100004
}
```

Concrete implementations:

- `WindowsPlatformIntegration` — returns OS-native defaults (no workarounds).
- `LinuxPlatformIntegration` — preserves RE-IMPLEMENT behaviors for X11/Wayland
  desktop environments.
- `MacOsPlatformIntegration` — preserves macOS-specific behaviors (e.g., `open`
  for URL launching).

### Reference-Compatibility Check Re-Enablement

The `NativeLib.IsUnix()` guard in `PluginManager.CheckCompatibilityPriv` has
been **removed** (WO-072). The reference-token check now runs on all platforms.
This is a deliberate, tested change — .NET 10 raises a proper `BadImageFormatException`
or `MissingMethodException` for invalid tokens on all platforms.

### Clipboard Workaround Migration

Workarounds #1530 and #1613 (clipboard reliability) were already hard-coded to
`false` before the modernization. They are classified OBSOLETE. The
`IClipboardService` abstraction (WO-040/WO-042) owns clipboard lifecycle on all
platforms. `LinuxClipboardService.DoClear()` preserves the KDE Klipper workaround
from #1613.

## Consequences

### Positive

- **No more dead-code guards**: all 45 `IsRequired(bugId)` call sites are
  removed. Platform behavior is expressed via typed interfaces, not untyped
  integer bug IDs.
- **Cross-platform security parity**: removing the Unix skip for bug #9604
  means the reference-token check runs on macOS and Linux, closing an
  asymmetric security gap.
- **Thread.Abort elimination**: the removed clipboard thread eliminates the
  last `Thread.Abort()` call site, making the codebase fully compatible with
  .NET 5+ thread semantics.
- **Explicit contracts**: `IPlatformIntegration` makes platform capability
  requirements visible in the type system; implementations are testable with
  mocks.

### Negative

- **Regression risk from broad removal**: retiring 36 workarounds at once
  risks silent behavioral regression. Each retired workaround must be
  individually verified against the acceptance criteria in WO-035. The
  full regression test suite (WO-083 through WO-087) provides automated
  coverage.
- **RE-IMPLEMENT workarounds require ongoing maintenance**: bug #19836
  (`OpenUrl`) and #100004 (native Argon2) require platform-specific code
  paths in `LinuxPlatformIntegration` and `MacOsPlatformIntegration`. These
  paths depend on external tools (`xdg-open`, native crypto libraries) which
  may not be installed on all target systems.
- **External-tool dependency**: bugs #190417 and #3471228285 (argument
  encoding) depend on the system's behavior of `Process.Start`. Regression
  requires testing on multiple Linux distributions and macOS versions.

### Neutral

- `MonoWorkarounds.cs` existed only in `KeePassLib`. Its removal does not
  affect the public `KeePassLib` API surface — `IsRequired(int)` was
  `internal`.
- The `Initialize()` / `Terminate()` lifecycle methods have no replacement.
  Platform integration is now registered at DI container setup time and
  torn down when the application exits normally.
- On a non-Mono, non-.NET runtime (future scenario), no workarounds are
  active by default. Capability probes in `IPlatformIntegration` must be
  updated if a new runtime exhibits platform-specific bugs.

## Edge Cases

1. **Security-relevant workaround (#9604)**: the Unix skip for the reference-
   token check is documented separately in the plugin trust model (ADR-003).
   Its removal required a targeted regression test verifying that a plugin
   with an invalid type reference is rejected on all three platforms.

2. **External-tool-dependent workarounds (#19836, #190417, #3471228285)**:
   `xdg-open` (Linux) and `open` (macOS) may not be installed on minimal
   server distributions. `IPlatformIntegration.OpenUrl` returns `false` when
   the required tool is absent, and callers fall back to a `ShellExecute`-
   style dialog or log a warning.

3. **`Initialize()` / `Terminate()` lifecycle**: `MonoWorkarounds.Initialize()`
   started the clipboard-fix background thread for #1530. On .NET 10 this
   thread is never started (OBSOLETE). Callers that called `Initialize()` at
   startup (e.g., `MainForm` constructor) have been updated to remove the call
   entirely. No replacement lifecycle call is needed.

## References

- [MonoWorkarounds-Classification.md](MonoWorkarounds-Classification.md) —
  complete per-workaround inventory with trigger conditions and migration targets
- `KeePass/Plugins/PluginManager.cs` — `CheckCompatibilityPriv`, removed
  `NativeLib.IsUnix()` guard (WO-072)
- `KeePass.Platform.Unix/LinuxPlatformIntegration.cs` — RE-IMPLEMENT targets
  for #1716 and #686017
- [ADR-003](ADR-003-plugin-trust-model.md) — plugin trust model (bug #9604 context)
- [ADR-000](ADR-000-template.md) — ADR template
- WO-035: MonoWorkarounds retirement implementation commit
- WO-040: `IClipboardService` extraction (supersedes #1530 / #1613)
- WO-043: MonoWorkaround analysis and classification
