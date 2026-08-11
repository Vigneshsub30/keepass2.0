# Rollback Runbook — Linux

This runbook covers rollback on Debian/Ubuntu (`.deb`), Fedora/RHEL (`.rpm`),
and AppImage distributions of KeePass.

---

## 1. Debian / Ubuntu — `.deb` Rollback

### Prerequisites

- The target `.deb` file (e.g. `keepass_<Version>_amd64.deb`) is available
  locally or downloadable from the GitHub Releases page.
- `sudo` access is required.

### Step 1 — Stop KeePass

```bash
pkill -f KeePass || true
```

### Step 2 — Install the previous `.deb`

`dpkg` downgrades when the target version is lower than the installed one:

```bash
sudo dpkg -i keepass_<Version>_amd64.deb
```

If there are unmet dependencies, resolve them with:

```bash
sudo apt-get install -f
```

### Step 3 — Hold the package at the rollback version

```bash
sudo apt-mark hold keepass
```

Remove the hold when you are ready to upgrade again:

```bash
sudo apt-mark unhold keepass
```

### Step 4 — Verify

```bash
keepass --version
dpkg -l keepass
```

---

## 2. Fedora / RHEL — `.rpm` Rollback

### Prerequisites

- The target `.rpm` file is available locally.
- `sudo` access is required.

### Step 1 — Downgrade the package

```bash
sudo rpm -Uvh --oldpackage keepass-<Version>-1.x86_64.rpm
```

### Step 2 — Exclude from automatic updates (dnf)

Add an exclusion to `/etc/dnf/dnf.conf`:

```ini
excludepkgs=keepass
```

Remove the line when ready to upgrade again.

### Step 3 — Verify

```bash
keepass --version
rpm -qi keepass
```

---

## 3. AppImage Rollback

AppImages are self-contained and do not interact with the system package manager,
making rollback straightforward.

### Step 1 — Stop KeePass

```bash
pkill -f KeePass.AppImage || true
```

### Step 2 — Rename (or remove) the current AppImage

```bash
mv ~/Applications/KeePass.AppImage ~/Applications/KeePass.AppImage.bak
```

### Step 3 — Place the previous AppImage

```bash
cp KeePass-<Version>-x86_64.AppImage ~/Applications/KeePass.AppImage
chmod +x ~/Applications/KeePass.AppImage
```

### Step 4 — Verify

```bash
~/Applications/KeePass.AppImage --version
```

Once confirmed, delete the backup:

```bash
rm ~/Applications/KeePass.AppImage.bak
```

---

## 4. Vault Recovery after a Failed Save

If KeePass reported a **post-commit integrity check failure**:

1. **Do not close KeePass** — the in-memory database is still intact.
2. Use **File → Save As…** to write the vault to a different path on a healthy
   filesystem.
3. Check the original directory for a leftover `.tmp` file from the transactional
   write — it may contain a recoverable copy.
4. Restore from the most recent KeePass backup
   (**Tools → Options → Advanced → Number of backup files to keep**).

---

## 5. MIME Type / Desktop Entry Reset

After a rollback, the `.kdbx` MIME-type association may point to the old path.
Re-register it:

```bash
# Update MIME database:
sudo update-mime-database /usr/share/mime

# Update desktop menu:
sudo update-desktop-database

# Update icon cache:
sudo gtk-update-icon-cache /usr/share/icons/hicolor
```

---

## 6. Contact

For persistent issues, file a bug at the project repository or reach out to
your organisation's IT administrator.
