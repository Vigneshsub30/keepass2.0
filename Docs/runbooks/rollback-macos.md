# Rollback Runbook — macOS

This runbook covers how to roll back a KeePass installation on macOS distributed
as a notarised `.dmg` containing a `.app` bundle.

---

## 1. DMG / .app Bundle Rollback

### Prerequisites

- The target `.dmg` file (e.g. `KeePass-<Version>-osx-arm64.dmg`) is available
  locally or downloadable from the GitHub Releases page.
- macOS 12 (Monterey) or later is assumed.

### Step 1 — Quit KeePass

```bash
osascript -e 'quit app "KeePass"'
```

Or use **⌘Q** inside the application.

### Step 2 — Remove the current application bundle

```bash
# Drag to Trash in Finder, or use the terminal:
rm -rf /Applications/KeePass.app
```

> **Note:** Your vault files and configuration are stored in
> `~/Library/Application Support/KeePass/` and are **not** affected by this
> step.

### Step 3 — Mount the previous DMG and install

```bash
# Mount the DMG:
hdiutil attach KeePass-<Version>-osx-arm64.dmg

# Copy the app bundle:
cp -R /Volumes/KeePass/KeePass.app /Applications/

# Eject the DMG:
hdiutil detach /Volumes/KeePass
```

### Step 4 — Clear Gatekeeper quarantine attribute

macOS attaches a quarantine attribute to files downloaded from the internet.
Remove it so the app launches without repeated security prompts:

```bash
xattr -d com.apple.quarantine /Applications/KeePass.app 2>/dev/null || true
```

### Step 5 — Verify

```bash
/Applications/KeePass.app/Contents/MacOS/KeePass --version
```

---

## 2. Homebrew Cask Rollback (if installed via Homebrew)

```bash
# Pin the current version to prevent auto-upgrade during rollback:
brew pin keepass

# Switch to a specific version via brew:
brew install keepass@<version>
```

If a versioned formula is unavailable, download the `.dmg` manually and follow
Section 1 above.

---

## 3. Vault Recovery after a Failed Save

If KeePass reported a **post-commit integrity check failure**:

1. **Do not quit KeePass** — the in-memory copy is still valid.
2. Choose **File → Save As…** and write the vault to a different location
   (e.g. your Desktop).
3. Check the original directory for a leftover `.tmp` file from the transactional
   write — it may contain a recoverable backup.
4. Restore from the most recent KeePass backup
   (**Tools → Options → Advanced → Number of backup files to keep**).

---

## 4. Reverting to a Specific Version with the Update Check Disabled

To prevent automatic update prompts while on a pinned version:

1. Open **Tools → Options → Advanced**.
2. Uncheck **Check for updates at startup**.
3. Also consider setting the release channel back to **Stable** if you were
   testing a Beta build.

---

## 5. Contact

For persistent issues, file a bug at the project repository or reach out to
your organisation's IT administrator.
