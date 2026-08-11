# Rollback Runbook — Windows

This runbook covers how to roll back a KeePass installation on Windows, either
from an MSI installer or a portable ZIP package.  Follow the procedure that
matches how KeePass was installed.

---

## 1. MSI Installer Rollback

### Prerequisites

- The version you want to restore must be available as a `.msi` file (either
  downloaded from the official release page or kept in a local archive).
- You must have local administrator rights.

### Step 1 — Uninstall the current version

```powershell
# Find the product code of the currently installed KeePass:
$app = Get-WmiObject -Class Win32_Product |
       Where-Object { $_.Name -like "KeePass*" }

# Uninstall silently:
msiexec /x "$($app.IdentifyingNumber)" /qn /norestart
```

Alternatively, from **Control Panel → Programs → Uninstall a program**, select
KeePass and click *Uninstall*.

> **Note:** Uninstalling does **not** delete user configuration files or vault
> files.  Your `.kdbx` databases and `KeePass.config.xml` remain in
> `%APPDATA%\KeePass\` (portable) or their original locations.

### Step 2 — Install the previous version

```powershell
# Replace <Version> and <Arch> with the target release, e.g. 2.61.0 / x64:
$msiPath = "KeePass-<Version>-<Arch>.msi"
msiexec /i "$msiPath" /qn /norestart
```

Verify the installation:

```powershell
(Get-ItemProperty "HKLM:\SOFTWARE\KeePass Password Safe 2").Version
```

### Step 3 — Open your vault

Launch KeePass and open your vault file (`.kdbx`).  If KeePass reports a
database format version incompatibility, use the previous backup made before
the upgrade.

---

## 2. Portable ZIP Rollback

### Prerequisites

- The previous version ZIP (e.g. `KeePass-<Version>-portable.zip`) is
  available locally or downloadable from the official release page.
- No administrator rights are required.

### Step 1 — Stop KeePass

Close the running instance completely (File → Exit).

### Step 2 — Rename the current portable folder

```powershell
Rename-Item -Path "C:\Tools\KeePass" -NewName "KeePass.bak"
```

This preserves the current binaries in case the rollback needs to be reversed.

### Step 3 — Extract the previous version

```powershell
Expand-Archive -Path "KeePass-<Version>-portable.zip" `
               -DestinationPath "C:\Tools\KeePass"
```

### Step 4 — Copy your configuration back (if needed)

If you stored `KeePass.config.xml` inside the portable folder, copy it from the
backup:

```powershell
Copy-Item -Path "C:\Tools\KeePass.bak\KeePass.config.xml" `
          -Destination "C:\Tools\KeePass\KeePass.config.xml"
```

### Step 5 — Verify and clean up

Launch `KeePass.exe` from the restored folder and confirm it loads correctly.
Once satisfied, you may delete the backup:

```powershell
Remove-Item -Recurse -Force "C:\Tools\KeePass.bak"
```

---

## 3. Vault Recovery after a Failed Save

If KeePass reported a **post-commit integrity check failure** (`VaultFileCorruptAfterSave`
or `VaultFileMissingAfterSave`):

1. **Do not close KeePass** — the in-memory database is still intact.
2. Use **File → Save As…** to write the vault to a different path on a healthy
   drive.
3. Inspect the original path for a `.tmp` file left by the transactional save;
   it may contain a recoverable copy.
4. Restore from the most recent KeePass backup
   (**Tools → Options → Advanced → Number of backup files to keep**).

---

## 4. Contact

For persistent rollback failures, open an issue at
<https://github.com/keepassxreboot/keepassxc/issues> (community) or contact
your organisation's IT administrator.
