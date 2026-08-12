# KeePass Smoke Test Checklist

This checklist covers manual verification steps that cannot be fully automated.
Run it before every release candidate on each supported platform.

## 1. Automated Pre-Check

Before running the manual steps, confirm that the automated smoke test passes:

```bash
# Windows
KeePass.exe --smoke-test

# macOS / Linux
./KeePass --smoke-test
```

Expected exit code: **0**.  Any non-zero exit code must be investigated and
resolved before continuing with manual steps.

---

## 2. Application Launch

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Launch KeePass normally (no arguments). | Main window appears without errors; title bar shows the correct version. | |
| Check the About dialog (**Help → About KeePass**). | Version string matches the release tag; copyright year is correct. | |
| Confirm beta indicator absent on stable builds. | Title bar must NOT contain `[Beta]` on a stable release build. | |
| Launch with a `.kdbx` file as an argument. | Key-prompt dialog appears for the specified file. | |

---

## 3. Database Operations

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Create a new database (**File → New**). | Key-creation wizard completes; new empty database opens. | |
| Add a group with a custom name and icon. | Group appears in the tree with the correct icon. | |
| Add an entry with Title, URL, Username, Password, Notes, and Expiry date. | Entry appears in the list; all fields visible in the Detail pane. | |
| Attach a binary file to an entry. | Attachment tab shows the filename; double-click opens the file. | |
| Save the database (**File → Save**). | No error dialog; file date on disk updates. | |
| Close and re-open the database. | All groups, entries, and attachments are present. | |
| Open one of the golden-file KDBX fixtures from `KeePass.Tests/Fixtures/GoldenKdbx/`. | Database opens; group count ≥ 2, entry count ≥ 5. | |

---

## 4. Entry Operations

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Copy password to clipboard (**Ctrl+C** on selected entry). | Clipboard contains the entry password; clipboard auto-clear notification appears (if configured). | |
| Open URL (**Ctrl+U** or double-click URL field). | Default browser opens to the entry URL. | |
| Generate a password using the built-in generator. | Generated password satisfies the configured policy; quality bar reflects strength. | |
| Edit an entry and save. | History entry appears in the History tab. | |
| Delete an entry to the recycle bin. | Entry moves to the Recycle Bin group; not permanently deleted. | |

---

## 5. Search

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Use Quick-Find (**Ctrl+F**). | Results panel shows matching entries; clearing the field restores the full list. | |
| Use Find (**Edit → Find**) with regex enabled. | Regex search returns correct matches. | |
| Filter by expiry (expired entries). | Only entries with past expiry dates appear. | |

---

## 6. Auto-Type (Windows only)

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Configure a global hotkey (**Tools → Options → Integration**). | Hotkey responds when focus is on a matching browser/application window. | |
| Trigger Auto-Type on an entry with a custom sequence. | Sequence executes correctly without garbled characters. | |

---

## 7. Visual Rendering and Accessibility

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Verify DPI scaling at 100%, 125%, 150%, and 200% (Windows). | All UI elements scale proportionally; no clipping or overlap. | |
| Verify dark mode / light mode (macOS and Linux where applicable). | Application adopts system theme; no colour contrast issues. | |
| Navigate the UI using keyboard only (Tab, arrow keys, Enter). | All interactive elements are reachable and operable without a mouse. | |
| Verify screen reader compatibility with a basic pass (e.g., NVDA on Windows, VoiceOver on macOS). | Key UI elements have accessible names; focus order is logical. | |

---

## 8. Platform-Specific

### Windows

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Verify single-instance enforcement: launch a second instance while the first is running and pass a `.kdbx` path. | Second instance forwards the path to the first instance and exits. | |
| Verify session lock notification: lock the Windows session, then unlock. | KeePass workspace locks and prompts on unlock if configured. | |

### macOS

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Open a `.kdbx` file from Finder. | KeePass receives the file open request and shows the key prompt. | |
| Verify notarisation: right-click the `.app` → **Get Info** → no quarantine message. | Gatekeeper accepts the application without prompting. | |

### Linux

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Verify MIME-type association: double-click a `.kdbx` file in the file manager. | KeePass opens and prompts for the master key. | |
| Verify clipboard operations: use `xclip` or `xsel` to confirm clipboard content after copy. | Clipboard content matches the entry password. | |

---

## 9. Update Check

| Step | Expected result | Pass / Fail |
|------|----------------|-------------|
| Trigger an update check (**Help → Check for Updates**). | Dialog appears with the current version; no error if the host is reachable. | |
| Verify beta channel (if applicable): set release channel to Beta, re-run update check. | `[Beta]` appears in the title bar; beta version URL is queried. | |

---

## 10. Sign-Off

| Field | Value |
|-------|-------|
| Tested by | |
| Platform | |
| OS version | |
| KeePass version | |
| Date | |
| Overall result | ☐ PASS  ☐ FAIL  ☐ PARTIAL |
| Notes | |
