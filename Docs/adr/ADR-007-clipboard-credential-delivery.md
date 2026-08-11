# ADR-007: Clipboard Credential Delivery Mechanism

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

Clipboard is the primary mechanism for copying passwords and other fields
from KeePass into target applications. On macOS and Linux it is the **only
confirmed v1 credential delivery mechanism** — auto-type and browser
extensions are not in scope for the initial .NET 10 release on those platforms.

The implementation spans three platform-specific files in `KeePass/Util/`:

| File | Platform | Mechanism |
|------|----------|-----------|
| `ClipboardUtil.cs` | All | Dispatch logic, ownership tracking, auto-clear |
| `ClipboardUtil.Windows.cs` | Windows | P/Invoke `OpenClipboard`/`SetClipboardData`, history suppression |
| `ClipboardUtil.Unix.cs` | macOS / Linux | `pbcopy` / `xsel` process execution |

### Copy Flow

1. The caller (typically `CopyToClipboard()` in `MainForm_Functions.cs`)
   invokes `ClipboardUtil.CopyAndMinimize(strData, …)`.
2. **Policy gate**: `AppPolicy.Try(AppPolicyId.CopyToClipboard)` is checked.
   If policy forbids copying, the call returns silently.
3. Platform dispatch:
   - **Windows**: `OpenW()`, `AttachIgnoreFormatsW()`, `SetDataW()`, `CloseW()`.
   - **macOS**: `NativeLib.RunConsoleApp("pbcopy", "-pboard general", str)`.
   - **Linux (xsel)**: `NativeLib.RunConsoleApp("xsel", "--input --clipboard", str)`.
4. **Ownership hash**: `g_pbDataHash` is set to `SHA-256(strData)` immediately
   after a successful copy (`ClipboardUtil.cs:108`).

### Windows Platform Implementation

`ClipboardUtil.Windows.cs` uses:

- `OpenClipboard(hOwner)` — acquires clipboard lock with **15 retries** at
  100 ms intervals (constant `CntUnmanagedRetries = 15`; the .NET framework
  default is 10). Contention with other clipboard-watching applications
  requires the extra retries.
- `EmptyClipboard()` — clears existing data.
- `SetClipboardData(CF_UNICODETEXT, hGlobal)` — sets the clipboard content
  as a global memory handle.
- `AttachIgnoreFormatsW()` — calls an undocumented Windows API to suppress
  clipboard history collection and cloud sync (OneDrive, Windows 11 clipboard
  history). This prevents credentials from appearing in the clipboard history
  pane or syncing to other devices.
- `CloseClipboard()` — releases the lock.

### macOS Platform Implementation

`ClipboardUtil.Unix.cs` dispatches to:

- **Write**: `pbcopy -pboard general < strData` — encodes the string as
  UTF-8 and pipes it to `pbcopy`.
- **Read** (for ownership check): `pbpaste -pboard general` — reads back the
  current clipboard content.

`pbcopy`/`pbpaste` are standard macOS system utilities available on all
supported versions.

### Linux Platform Implementation

`ClipboardUtil.Unix.cs` dispatches to:

- **Write**: `xsel --input --clipboard < strData` — encodes the string as
  UTF-8 and pipes it to `xsel`.
- **Read** (for ownership check): `xsel --output --clipboard` — reads back
  the clipboard content.

**`xsel` must be installed separately.** On minimal distributions it may not
be present, causing clipboard operations to fail silently.

Workaround #1613 (Mono clipboard via `xdotool` FixClipThread) has been
**retired on .NET 10** — the background xdotool thread is no longer started
(`ClipboardUtil.Unix.cs:59-73`).

### Auto-Clear Mechanism

A countdown timer in `MainForm` (managed by `ClipboardCredentialService`,
extracted from `MainForm` in WO-076) fires every second:

1. `m_nClipClearCur` decrements by 1 each tick.
2. When `m_nClipClearCur` reaches 0, `ClearIfOwner()` is called.

`ClearIfOwner()` (`ClipboardUtil.cs:268`):

1. Reads the current clipboard text (platform-specific read path).
2. Computes `SHA-256(currentText)`.
3. Compares against `g_pbDataHash` (the hash stored at copy time).
4. If equal, clears the clipboard; otherwise, skips clearing.
5. Sets `g_pbDataHash = null` regardless.

The SHA-256 comparison prevents KeePass from clearing clipboard content
that another application has since replaced. This is best-effort — an
attacker who copies the same string can cause the clear to fire early,
but cannot prevent a clear by copying different content.

### KDE / Klipper Special Handling

KDE's Klipper clipboard manager maintains its own clipboard history. A single
`ClearClipboard()` call is insufficient because Klipper restores the previous
clipboard entry. The workaround:

```
Repeat 6 times:
  Clear clipboard (OpenW + EmptyClipboard + CloseW)
  Wait until 125 ms have elapsed since the start of this iteration
```

This forces Klipper to acknowledge the clear before it can restore the
previous entry. The iteration count (6) and delay (125 ms) were determined
empirically.

### Clipboard Credential Service (WO-076 Extraction)

The auto-clear timer logic and countdown state (`m_nClipClearCur`,
`m_nClipClearMax`) were extracted from `MainForm` into
`ClipboardCredentialService` (`KeePass/Services/ClipboardCredentialService.cs`)
in WO-076. `MainForm` now delegates to this service. The service fires
`AutoClearTimerTick()` from the existing `MainForm` 1-second timer.

## Decision

**Clipboard with auto-clear is the v1 credential delivery mechanism for
macOS and Linux.** Auto-type and browser extension integration are planned
for future releases but are out of scope for the initial .NET 10 port.

The current platform-specific implementation is retained as-is for the .NET
10 port, with the following modifications:

1. **xdotool FixClipThread retired**: Mono workaround #1613 is removed
   (dead code on .NET 10). `xsel` remains the Linux clipboard tool.
2. **ClipboardCredentialService extraction**: auto-clear logic lives in a
   dedicated service rather than `MainForm`.
3. **AttachIgnoreFormatsW retained**: the undocumented Windows API call is
   kept because its effect (history/cloud-sync suppression) is critical for
   security. If the API is removed by a future Windows update, clipboard
   history suppression will silently degrade — this risk is accepted.

## Consequences

### Positive

- **Simplest cross-platform baseline**: `pbcopy` (macOS) and `xsel`
  (Linux) are widely available on desktop systems and require no additional
  privileges.
- **SHA-256 ownership tracking prevents accidental clear of other apps'
  content**: if the user copies something else before the auto-clear fires,
  KeePass correctly skips the clear.
- **AttachIgnoreFormatsW prevents Windows 11 clipboard history**: credentials
  copied on Windows 11 do not appear in the clipboard history pane or sync
  to other devices.
- **KDE Klipper handled**: the 6×125ms loop prevents Klipper from restoring
  a cleared credential.

### Negative

- **Clipboard is inherently observable**: any process running under the
  same user account can read clipboard contents at any time. Auto-clear is
  a best-effort mitigation, not a guarantee. A clipboard-monitoring process
  can capture credentials before the clear fires.
- **External tool dependency on Linux**: `xsel` is not installed by default
  on all distributions. Clipboard operations fail silently when `xsel` is
  absent. There is no fallback to `xclip`, `wl-copy` (Wayland), or other
  tools in v1.
- **15-retry Windows contention window**: during the 15×100ms = 1.5s retry
  window, clipboard access from another application blocks KeePass from
  writing. The credential copy appears to succeed (no error shown) but may
  not reach the clipboard.
- **AttachIgnoreFormatsW is undocumented**: reliance on an undocumented
  Windows API is a fragility risk. Microsoft may remove or change it in a
  future Windows update.
- **Wayland not supported in v1**: `xsel` requires an X11 session. Pure
  Wayland sessions (without XWayland) cannot use clipboard.

### Neutral

- The auto-clear timeout is configurable via `AceSecurity.ClipboardClearAfterSeconds`.
  The default is 10 seconds.
- Binary data (attachments) is **not** transported via clipboard in v1.
  The WO technical details mention data URI encoding as a future path.

## Edge Cases

1. **Another application takes clipboard ownership between copy and auto-clear**:
   `ClearIfOwner()` reads the current clipboard content and computes its
   SHA-256. If the hash does not match `g_pbDataHash`, the clear is skipped.
   The credential is not re-cleared on subsequent ticks — `g_pbDataHash` is
   set to `null` after the first tick regardless.

2. **`xsel` not installed on Linux**: `NativeLib.RunConsoleApp("xsel", …)`
   returns `null` when the process fails to start. The clipboard write silently
   fails. `g_pbDataHash` is still set (preventing a future spurious clear) but
   the credential never reached the clipboard. No user-visible error is shown
   in v1. Future work should check `xsel --version` at startup and warn the
   user.

3. **Windows retry loop (15 retries, 100ms delay)**: if another application
   holds the clipboard open for more than 1.5 seconds, all 15 retries fail and
   the clipboard write is abandoned. `g_pbDataHash` is not set in this case
   (the copy failed at the OS level). The user sees no error — this is a silent
   failure matching the original behavior.

4. **Binary data clipboard transport**: binary attachments are not copyable
   to the clipboard via the text path. The `ClipboardUtil` API only exposes
   `SetData(string)`. Attachment copy requires a separate code path (not yet
   implemented for the cross-platform head).

## References

- `KeePass/Util/ClipboardUtil.cs` — dispatch, `g_pbDataHash`, `ClearIfOwner()`
- `KeePass/Util/ClipboardUtil.Windows.cs` — P/Invoke flow, `CntUnmanagedRetries`,
  `AttachIgnoreFormatsW`
- `KeePass/Util/ClipboardUtil.Unix.cs` — `pbcopy`, `pbpaste`, `xsel` paths
- `KeePass/Services/ClipboardCredentialService.cs` — auto-clear service
  (extracted from `MainForm` in WO-076)
- [ADR-005](ADR-005-mono-workaround-retirement.md) — Mono workaround #1613
  retirement (xdotool FixClipThread)
- [ADR-000](ADR-000-template.md) — ADR template
