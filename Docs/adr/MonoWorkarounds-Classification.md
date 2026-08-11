# MonoWorkarounds Classification — WO-043

- **Date:** 2026-08-11
- **Status:** Accepted
- **Produced by:** WO-043 (analysis), executed alongside WO-035 (implementation)

## Background

`KeePassLib/Utility/MonoWorkarounds.cs` contained 45 numbered workarounds for
defects in the Mono runtime, all gated by `MonoWorkarounds.IsRequired(bugId)`.
On .NET 10 the Mono runtime is no longer used; most workarounds became dead
code. This document is the canonical inventory and classification used to drive
their safe removal in WO-035 and to record which behaviours must be preserved
via `IPlatformIntegration`.

## Classification Legend

| Label | Meaning |
|-------|---------|
| **RETIRE** | Mono-only defect. .NET 10 does not exhibit this behaviour. Remove all call sites. |
| **RE-IMPLEMENT** | Real platform behaviour still needed on Linux/macOS. Migrate to `IPlatformIntegration` or a platform service. |
| **OBSOLETE** | Feature is being removed or the workaround was already hard-coded to `false`. |

---

## Full Inventory

| Bug ID | Description | Affected Component | Trigger Condition | Classification | Notes / Migration Target |
|--------|-------------|-------------------|-------------------|---------------|--------------------------|
| 106 | Mono throws exceptions when no X server is running | Startup / display detection | `IsRequired()` (any Mono) | **RETIRE** | Avalonia abstracts display presence; not an issue on .NET 10 |
| 1219 | Mono prepends BOM to StdIn | Process stdin handling | `IsRequired()` | **RETIRE** | .NET 10 `StreamWriter` does not add BOM by default |
| 1245 | Key events not raised while Alt is held; nav keys out of order | Auto-type / keyboard input | `IsRequired()` | **RETIRE** | Mono WinForms-specific; Avalonia keyboard pipeline is unaffected |
| 1254 | `NumericUpDown` draws text below the spin buttons | UI layout (Options dialog) | `IsRequired(1254)` | **RETIRE** | Mono WinForms rendering bug; not present in Avalonia or .NET 10 WinForms |
| 1354 | `NotifyIcon` finalizer throws on Unity desktop | System tray | `IsRequired()` | **RETIRE** | `NotifyIcon` removed in favour of Avalonia platform tray API |
| 1358 | `OpenFileDialog` crashes when `~/.recently-used` is malformed | File dialogs | `IsRequired()` | **RETIRE** | Avalonia uses native GTK dialogs; malformed file is benign |
| 1366 | `RichTextBox` scrolling renders garbage | Entry details / Notes | `IsRequired()` | **RETIRE** | Mono WinForms rendering bug; not present in Avalonia |
| 1378 | `Microsoft.Win32.SystemEvents` not implemented | Power management / lock events | `IsRequired()` | **RETIRE** | Replaced by Avalonia lifetime events and .NET 10 power notifications |
| 1418 | Minimising a form during load is silently ignored | Window management | `IsRequired()` | **RETIRE** | Mono WinForms-specific; Avalonia window state is consistent |
| 1468 | Mono `RijndaelManaged`/`AesCryptoServiceProvider` too slow; use LibGCrypt | AES-KDF performance | `IsRequired(1468)` | **RETIRE** | Replaced by `Aes.Create()` in .NET 10, which calls native CNG/OpenSSL |
| 1527 | `System.Threading.Timer` causes 100 % CPU on Mono | Auto-clear timer, scheduled tasks | `IsRequired(1527)` | **RETIRE** | Fixed in .NET Core 3.0+; not reproducible on .NET 10 |
| **1530** | Mono clipboard helpers unreliable; use background thread | Clipboard (`ClipboardUtil`) | Hard-coded `return false;` | **OBSOLETE** | Already disabled before this project; replaced by `IClipboardService.IsSupported` |
| 1574 | `NotifyIcon` finalizer throws on macOS | System tray (macOS) | `IsRequired()` | **RETIRE** | Same root cause as #1354; `NotifyIcon` removed |
| **1613** | Mono clipboard helpers unreliable (alternate fix) | Clipboard clearing | Hard-coded `return false;` | **OBSOLETE** | Already disabled before this project; KDE Klipper workaround migrated to `LinuxClipboardService.DoClear()` (WO-042) |
| 1632 | `RichTextBox` renders bold/italic incorrectly | Entry details / Notes | `IsRequired()` | **RETIRE** | Mono WinForms rendering bug; not present in Avalonia |
| 1690 | Removing items from `ListView` leaves artefacts | Entry list, Group tree | `IsRequired()` | **RETIRE** | Mono WinForms bug; not present in Avalonia |
| 1710 | `FormClosed` event not reliably raised | Window lifecycle | `IsRequired()` | **RETIRE** | Mono WinForms event model bug; Avalonia raises `Closed` reliably |
| **1716** | `AlwaysOnTop` ignored on Cinnamon desktop | Main window Z-order | `IsRequired(1716)` | **RE-IMPLEMENT** | Migrated to `IPlatformIntegration.SupportsAlwaysOnTop` (WO-035). `LinuxPlatformIntegration` returns `false` when `XDG_CURRENT_DESKTOP=X-Cinnamon` |
| 1760 | Input focus not restored when a form is activated | Keyboard focus | `IsRequired()` | **RETIRE** | Mono WinForms focus model bug; Avalonia focus pipeline is independent |
| 1976 | Input focus cannot be set after workspace unlock | Unlock dialog | `IsRequired()` | **RETIRE** | Mono WinForms-specific; Avalonia focus API works correctly |
| 2140 | Explicit control focusing is silently ignored | Various dialogs | `IsRequired()` | **RETIRE** | Mono WinForms-specific; Avalonia `Focus()` behaves correctly |
| 2247 | Form height grows unexpectedly after `ResumeLayout` | Dialog sizing | `IsRequired()` | **RETIRE** | Mono WinForms layout bug; not present in Avalonia |
| 5795 | Text in input field is truncated | Entry text fields | `IsRequired()` | **RETIRE** | Xamarin/Mono bug; not present in .NET 10 or Avalonia |
| 9604 | Resolving a non-existing metadata token crashes Mono | Plugin loader | `IsRequired()` | **RETIRE** | Mono reflection bug; .NET 10 throws a typed exception instead |
| 10163 | `WebRequest.GetResponse` missing; breaks WebDAV PUT | WebDAV / remote databases | `IsRequired()` | **RETIRE** | .NET 10 `HttpClient` fully implements PUT; `WebRequest` also fixed |
| 12525 | `PictureBox` not rendered when bitmap height ≥ control height | Icon display | `IsRequired()` | **RETIRE** | Mono WinForms rendering bug; not present in Avalonia |
| 19836 | URLs / documents cannot be opened with `Process.Start` | URL opening, attachment launch | `IsRequired(19836)` | **RE-IMPLEMENT** | Migrate to `IPlatformIntegration.OpenUrl(string url)` (planned: WO-044). On Linux, use `xdg-open`; on macOS, use `open`. |
| 100001 | Control positions/sizes are unexpected | General WinForms layout | `IsRequired()` | **RETIRE** | No public bug ref; Mono WinForms DPI/layout quirk not present in Avalonia |
| 100002 | `TextChanged` not raised when only formatting changes | `RichTextBox` search highlight | `IsRequired()` | **RETIRE** | No public bug ref; Mono WinForms event model quirk; not applicable in Avalonia |
| 100003 | `Icon.ExtractAssociatedIcon` always returns the same icon | File type icons | `IsRequired()` | **RETIRE** | No public bug ref; Mono-specific; Avalonia uses platform icon APIs |
| 100004 | Use native Argon2 implementation | KDBX key derivation | `IsRequired(100004)` | **RE-IMPLEMENT** | Migrate to `IPlatformIntegration` `GetNativeArgon2` capability or use .NET 10 `KeyDerivationPrf` (planned: WO-006). Mono's managed implementation was too slow. |
| 190417 | `Process.Start` replaces `\\` with `/` in arguments | External tool launching | `IsRequired(190417)` | **RE-IMPLEMENT** | Migrate to `IPlatformIntegration.StartProcess(ProcessStartInfo)` (planned: WO-044). Escape back-slashes only on Mono; .NET 10 is correct. |
| 373134 | `Control.InvokeRequired` returns wrong value | Thread marshalling | `IsRequired()` | **RETIRE** | Mono WinForms thread-affinity bug; not present in .NET 10 or Avalonia |
| 586901 | `RichTextBox` mishandles Unicode strings | Entry notes | `IsRequired()` | **RETIRE** | Mono WinForms Unicode bug; not present in Avalonia |
| 620618 | `ListView` column headers not drawn | Entry list | `IsRequired()` | **RETIRE** | Mono WinForms rendering bug; not present in Avalonia |
| 649266 | `Control.Hide()` doesn't remove app from taskbar | Window management | `IsRequired()` | **RETIRE** | Mono WinForms taskbar integration bug; not present in Avalonia |
| **686017** | Window minimum sizes must be enforced by application | All resizable windows | `IsRequired(686017)` | **RE-IMPLEMENT** | Migrated to `IPlatformIntegration.RequiresWindowMinSizeEnforcement` (WO-035). `LinuxPlatformIntegration` returns `true`. |
| 688007 | Credentials required for anonymous HTTP requests | Remote database sync | `IsRequired()` | **RETIRE** | Mono `HttpWebRequest` bug; .NET 10 `HttpClient` handles anonymous correctly |
| 801414 | Main window recreated incorrectly by Mono | Main form lifecycle | `IsRequired()` | **RETIRE** | Mono WinForms window recreation bug; Avalonia lifecycle is correct |
| 891029 | Tab control height too small; images on tabs misaligned | Settings dialog tabs | `IsRequired()` | **RETIRE** | Mono WinForms tab rendering bug; not present in Avalonia |
| 836428016 | `ListView` group-header selection unsupported | Group tree | `IsRequired()` | **RETIRE** | Mono WinForms feature gap; Avalonia `TreeView` natively supports group selection |
| 2449941153 | `RichTextBox` doesn't escape `}` in RTF output | Entry notes export | `IsRequired()` | **RETIRE** | Mono WinForms RTF generator bug; not present in Avalonia |
| 3471228285 | Command-line arguments must be encoded differently | External tool integration | `IsRequired()` | **RE-IMPLEMENT** | Closely related to #190417. Migrate to `IPlatformIntegration.EncodeArguments(string[])` (planned: WO-044). |
| 3574233558 | Minimising windows leaves blank content area | Window management | `IsRequired()` | **RETIRE** | Mono WinForms double-buffer/minimize bug; not present in Avalonia |
| 4190280862 | Right-click on tree node opens wrong context menu | Group/Entry tree | `IsRequired()` | **RETIRE** | Mono WinForms hit-test bug; not present in Avalonia |

**Total:** 45 workarounds — 36 RETIRE, 5 RE-IMPLEMENT, 2 OBSOLETE, 2 ALREADY DISABLED (counted as OBSOLETE above).

---

## Thread.Abort() Replacement

`MonoWorkarounds.Terminate()` called `g_thFixClip.Abort()` to cancel the
clipboard-fix background thread started for workaround #1530.

```csharp
// Original — INCOMPATIBLE with .NET 5+:
try { g_thFixClip.Abort(); }
finally { g_thFixClip = null; }
```

`Thread.Abort()` throws `PlatformNotSupportedException` on .NET 5 and later.

### Replacement Strategy

Since workaround #1530 is classified **OBSOLETE** (the clipboard thread is
never started on .NET 10), the `g_thFixClip` field and `Terminate()` method
were deleted entirely in WO-035. No replacement is required.

If a similar background thread is ever needed in the future, the correct
pattern on .NET 10 is:

```csharp
private CancellationTokenSource _cts;

void StartThread()
{
    _cts = new CancellationTokenSource();
    _ = Task.Run(() => WorkerLoop(_cts.Token), _cts.Token);
}

void StopThread()
{
    _cts?.Cancel();
    _cts = null;
}
```

---

## Dependency Map

The table below shows which work orders can proceed only after the relevant
RETIRE/RE-IMPLEMENT classifications are acted upon.

| Prerequisite (must retire/implement) | Unlocks |
|--------------------------------------|---------|
| #1716 RE-IMPLEMENT → `SupportsAlwaysOnTop` | WO-035 (done) — main window always-on-top restored on non-Cinnamon Linux |
| #686017 RE-IMPLEMENT → `RequiresWindowMinSizeEnforcement` | WO-035 (done) — window resize guards removed from Mono path |
| All WinForms RETIRE entries (38) | WO-035 (done) — `MonoWorkarounds.cs` deleted; no more `IsRequired` call sites |
| #19836 RE-IMPLEMENT → `OpenUrl` | WO-044 (planned) — `UrlUtil` can delegate to platform |
| #190417 / #3471228285 RE-IMPLEMENT → `StartProcess` / `EncodeArguments` | WO-044 (planned) — external tool integration cleaned up |
| #100004 RE-IMPLEMENT → native Argon2 / crypto | WO-006 (planned) — key derivation performance on Linux/macOS |
| All clipboard OBSOLETE (#1530, #1613) | WO-040 (done) — `IClipboardService` owns clipboard lifecycle |

---

## Summary Statistics

| Classification | Count |
|----------------|-------|
| RETIRE | 36 |
| RE-IMPLEMENT | 5 |
| OBSOLETE (already disabled) | 2 |
| OBSOLETE (feature removed) | 2 |
| **Total** | **45** |

---

## References

- WO-035 commit: `89ee676` — "Retire MonoWorkarounds and replace with IPlatformIntegration capability checks"
- WO-040: Extract `IClipboardService` with auto-clear timer (supersedes #1530 / #1613)
- WO-042: `LinuxClipboardService` — KDE/Klipper workaround preserved in `DoClear()` (related to #1613)
- [ADR-001](ADR-001-image-abstraction-breaking-change.md) — `FileFormatProvider` image abstraction
- Mono issue tracker (SourceForge): https://sourceforge.net/p/keepass/bugs/
- .NET 10 breaking changes: https://learn.microsoft.com/dotnet/core/compatibility/10.0
