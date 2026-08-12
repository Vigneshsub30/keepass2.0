# ADR-008: Atomic-Save Transaction Guarantees

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

A single KeePass save operation writes a re-encrypted copy of the entire
vault to disk. Any interruption (power failure, OS crash, full disk) during
the write must leave either the previous file intact or the new file complete
— never a partial file. The implementation is in
`KeePassLib/Serialization/FileTransactionEx.cs`.

### Three-Tier Transaction Strategy

`CommitWriteTransaction()` attempts three strategies in order, falling back
on each failure:

| Tier | Strategy | Platform | Condition |
|------|----------|----------|-----------|
| 1 | NTFS Transactional File System (TxF) | Windows NTFS only | `TxfMoveWithTx()` — `CreateTransaction` + `MoveFileTransacted` + `CommitTransaction` via P/Invoke |
| 2 | Two-phase `MoveFileEx` | Windows (and cross-platform fallback) | `TxfMove()` — write to intermediate file, then two `MoveFileEx` calls with `MOVEFILE_COPY_ALLOWED \| MOVEFILE_REPLACE_EXISTING` |
| 3 | Simple rename | All platforms | `File.Move` on POSIX-safe filesystems |

**Tier 1 (TxF)** wraps the file move in a kernel transaction. If the
transaction does not commit, the original file remains untouched. TxF
was deprecated by Microsoft but remains functional on Windows NTFS.

**Tier 2 (two-phase MoveFileEx)** introduces an intermediate file on the
same drive. The sequence:
1. Move temp file → intermediate file (`MOVEFILE_COPY_ALLOWED` allows
   cross-drive if the temp is on a different volume).
2. Move intermediate file → base file (atomic on NTFS within the same
   volume).

If power is lost between steps 1 and 2, the intermediate file exists but
the base file is intact. On next save, ClearOld() cleans up old
intermediate files.

**Tier 3 (POSIX rename)** uses `rename(2)` semantics on macOS/Linux. POSIX
guarantees that `rename` is atomic within the same filesystem. If the
source and destination are on different filesystems, the kernel falls back
to copy+delete (not atomic).

### Disable Conditions for TxF (`TxfPrepare()` returns without setting up TxF)

| Condition | Code Location | Reason |
|-----------|---------------|--------|
| Symbolic link (`FileAttributes.ReparsePoint`) | `FileTransactionEx.cs:100` | TxF would replace the symlink itself, not the target |
| Base file does not exist (new database) | Implicit: `!File.Exists(m_iocBase.Path)` | ACL inheritance from the temp directory would be wrong |
| FTP URL under .NET 4.0 | `FileTransactionEx.cs:120` | Framework bug #621450; `MoveFileEx` over FTP was broken |
| Unix (`NativeLib.IsUnix()`) | `FileTransactionEx.cs:358` | TxF is Windows NTFS-only P/Invoke |
| Windows 10 1809 + OneDrive path | `FileTransactionEx.cs:455–470` | Windows 10 1809 OneDrive crash bug; detected via `ReleaseId` registry key |

### Metadata Preservation

After the successful move, `CommitWriteTransaction()` restores file
metadata from values saved before the write:

| Metadata | Storage | Restore |
|----------|---------|---------|
| File creation time | `DateTime? otCreation` | `File.SetCreationTimeUtc()` |
| NTFS file statistics | `SimpleStat sStat` | `SimpleStat.Set()` |
| NTFS ACL (DACL) | `byte[] pbSec` via `FileSecurity.GetSecurityDescriptorBinaryForm()` | `FileSecurity` binary form applied to new file |
| EFS encryption state | `bool bEfsEncrypted` | `File.Encrypt()` after move |

NTFS ACLs are read via `FileInfo.GetAccessControl()` and stored as a raw
binary descriptor. On Unix, `GetAccessControl()` is not supported and the
catch block silently skips ACL preservation (`Debug.Assert(NativeLib.IsUnix())`).

### ExtraSafe Mode

When `FileTransactionEx.ExtraSafe = true` (set by test infrastructure),
the commit verifies that the temp file exists before attempting the move.
If the temp file is missing, the commit fails with an assertion error.

### Plugin / Per-Path Override

`FileTransactionEx.Configure(string strPrefix, bool? obTransacted)` inserts
a per-prefix entry into a static dictionary (`g_dEnabled`). Plugins or
administrative configuration can disable transactions for specific path
prefixes (e.g., network shares where TxF is unavailable).

### ClearOld()

`FileTransactionEx.ClearOld()` deletes intermediate temp files (extension
`.tmp`) in the database's directory that are older than 1 day. This cleans
up orphaned files from previously interrupted transactions.

## Decision

The three-tier strategy is retained for the .NET 10 port with the following
adjustments:

1. **Tier 1 (TxF) retained as a Windows NTFS optimization.** TxF is deprecated
   by Microsoft but remains functional and provides the strongest durability
   guarantee on Windows NTFS volumes. It is retained because its disable
   conditions already exclude all platforms and configurations where it might
   fail.

2. **Tier 2 (two-phase MoveFileEx) is the universal cross-platform fallback.**
   On macOS and Linux, `MOVEFILE_COPY_ALLOWED | MOVEFILE_REPLACE_EXISTING` is
   not available (Windows-only flags). The two-phase fallback on Unix uses
   `File.Move(sourceDest, replace: true)` (available in .NET 5+), which maps
   to `rename(2)` on POSIX systems. `rename(2)` is atomic within the same
   filesystem.

3. **ACL preservation on Unix is a no-op.** The `catch` around
   `GetAccessControl()` on Unix is correct. Unix permissions are preserved
   by `rename(2)` inherently (inode metadata is unchanged).

4. **EFS encryption state is a Windows-only concern.** The
   `File.Encrypt()`/`File.Decrypt()` path is guarded by `FileAttributes.Encrypted`
   which is only set on Windows NTFS.

5. **ClearOld() is cross-platform safe.** It deletes `.tmp` files by date;
   no platform-specific APIs are used.

## Consequences

### Positive

- **No data loss on power failure**: a partial write leaves the original
  file intact on all three tiers because the write occurs to a temp file
  before any atomic swap.
- **Metadata preservation across Windows upgrades**: file creation time and
  ACL are restored, preventing timestamp or permission surprises after save.
- **POSIX `rename(2)` atomicity on Linux/macOS**: within the same filesystem,
  the new file appears atomically; readers never see a partial file.

### Negative

- **TxF is deprecated by Microsoft**: Microsoft has stated that TxF may be
  removed in a future Windows version. If removed, the code falls back to
  Tier 2 (which is already tested and correct).
- **Cross-filesystem saves are not atomic**: if the KeePass temp directory
  (`Path.GetTempPath()`) is on a different filesystem than the database file,
  `rename(2)` falls back to copy+delete (not atomic). Contributors should
  ensure the temp file is written to the same filesystem as the database.
- **OneDrive 1809 TxF crash detection is brittle**: the registry
  `ReleaseId = "1809"` check is hardcoded. If Microsoft ships a future build
  that reproduces the bug with a different `ReleaseId`, TxF will be used and
  may crash. The risk is low (the bug is specific to Windows 10 1809 + OneDrive).

### Neutral

- The EFS encryption save/restore adds two filesystem round-trips (decrypt
  before move, re-encrypt after) on encrypted databases. This is intentional:
  TxF cannot operate on EFS-encrypted files.
- `Configure(prefix, bool?)` is currently only used by test infrastructure.
  It is retained as a documented extension point for future administrative
  tooling.

## Edge Cases

1. **TxF commit fails mid-transaction**: if `CommitTransaction()` fails,
   the kernel rolls back the kernel transaction. The original base file is
   untouched. The temp file remains on disk. `ClearOld()` removes it on the
   next successful save.

2. **Two-phase fallback with source and destination on different drives**:
   `MOVEFILE_COPY_ALLOWED` allows `MoveFileEx` to copy across drives before
   deleting the source. The intermediate file is created first, then the
   intermediate-to-base move is an intra-volume atomic rename. This means
   the base file is always in a consistent state — either the old version or
   the new version, never partial.

3. **Windows 10 1809 OneDrive TxF crash**: `TxfIsUnusable()` checks the
   `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ReleaseId` registry
   value. If it equals `"1809"`, TxF is disabled and Tier 2 is used. The
   registry key is available only on Windows; on other platforms
   `Registry.GetValue` returns null and `TxfIsUnusable()` returns `false`
   (TxF not unusable by this check — but TxF is already excluded on Unix by
   the `NativeLib.IsUnix()` check).

4. **New database (base file does not exist)**: `TxfPrepare()` does not
   enable TxF because ACL inheritance from the database's parent directory
   cannot be guaranteed if TxF moves the temp file (which inherits from the
   temp directory). Tier 2 or 3 is used instead, and the new file inherits
   ACLs normally from the parent directory.

## References

- `KeePassLib/Serialization/FileTransactionEx.cs` — complete implementation:
  `CommitWriteTransaction()`, `TxfPrepare()`, `TxfMove()`, `TxfMoveWithTx()`,
  `TxfIsUnusable()`, `Configure()`, `ClearOld()`
- `KeePassLib/Serialization/IOConnection.cs` — `FileTransactionEx` usage in
  the write path
- [ADR-000](ADR-000-template.md) — ADR template
- Microsoft TxF documentation: https://learn.microsoft.com/windows/win32/fileio/transactional-ntfs-portal
