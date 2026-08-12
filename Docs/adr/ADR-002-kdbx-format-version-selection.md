# ADR-002: KDBX Format-Version Selection Logic

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

KeePass stores vaults in the KDBX binary format. Three major KDBX
versions are in active use:

| Version | Constant | Value |
|---------|----------|-------|
| KDBX 3.1 | `FileVersion32_3_1` | `0x00030001` |
| KDBX 4.0 | `FileVersion32_4` | `0x00040000` |
| KDBX 4.1 | `FileVersion32_4_1` | `0x00040001` |

The **critical mask** (`FileVersionCriticalMask = 0xFFFF0000`) extracts
the major version component. A reader that does not recognise a major
version must refuse to open the file. Minor-version differences (lower
16 bits) are backwards-compatible within the same major family.

KDBX 4 (and 4.1) introduced an **inner header** that supersedes several
outer-header fields (cipher, compression, inner stream ID/key). The
`ICipherEngine` / `ICipherEngine2` interface hierarchy mirrors this split:
engines that implement `ICipherEngine2` provide an inner cipher for the
inner stream.

**KDBX 3.1 limitations** — the following features require KDBX ≥ 4 and
cannot be expressed in 3.1:

| Feature | Introduced in |
|---------|---------------|
| ChaCha20 cipher | KDBX 4 |
| Argon2d / Argon2id KDF | KDBX 4 |
| `PublicCustomData` (outer header VariantDictionary) | KDBX 4 |
| Per-entry and per-group `CustomData` dictionaries | KDBX 4 |
| `QualityCheck = false` on entries | KDBX 4.1 |
| Group `Tags` | KDBX 4.1 |
| Custom icon `Name` or `LastModificationTime` | KDBX 4.1 |
| Database-level `CustomData` entries with `LastModificationTime` | KDBX 4.1 |

A user who enables any 4.1 feature unknowingly produces a file that
older KeePass 2.x clients (before 2.50) cannot open. This has support
and data-recovery implications.

## Decision

The minimum KDBX version for a save operation is **determined purely by
the content of the vault**, not by the previous version of the file or
any user preference. The algorithm (`KdbxFile.GetMinKdbxVersion` in
`KeePassLib/Serialization/KdbxFile.cs`) walks the database tree and
returns the lowest version that can faithfully represent the current
content:

1. **Traverse all groups** — if any group has `Tags.Count != 0`, floor
   the minimum at KDBX 4.1.
2. **Traverse all entries** — if any entry has `QualityCheck == false`,
   floor the minimum at KDBX 4.1.
3. If the minimum is already 4.1, return immediately (items 4 and 5 add
   nothing).
4. **Scan custom icons** — if any `PwCustomIcon` has a non-empty `Name`
   or a populated `LastModificationTime`, return KDBX 4.1.
5. **Scan database-level CustomData** — if any key has a recorded
   `LastModificationTime`, return KDBX 4.1.
6. If none of the above triggers fired, **return KDBX 4.0** (the default
   minimum; KDBX 3.1 is never chosen automatically).

The format version is **never upgraded gratuitously**. If a vault
previously written at KDBX 4.1 has all 4.1-requiring features removed,
the next save will still produce KDBX 4.0 (not 4.1 and not 3.1),
because the algorithm returns the minimum version needed — it does not
remember the prior version.

An override hook (`KdbxFile.ForceVersion`) exists to force a specific
version without walking the tree; it is used exclusively by the
`KeePassKdbx2v3` export path (see below).

### KDBX 3.1 Compatibility Export

`KeePassKdbx2v3.Export` (`KeePass/DataExchange/Formats/KeePassKdbx2.cs`)
provides a dedicated export path for users who need a file that older
clients can open. The exporter:

1. **Downgrades cipher**: ChaCha20 → AES-256-CBC.
2. **Downgrades KDF**: Argon2 → AES-KDF (AesKdf).
3. **Removes `PublicCustomData`** (outer header VariantDictionary).
4. **Removes per-entry and per-group `CustomData`** dictionaries.
5. Forces `ForceVersion = FileVersion32_3_1` so `GetMinKdbxVersion` is
   bypassed entirely.
6. After writing, **restores** the original cipher, KDF, and custom data
   on the in-memory database (the export is non-destructive).

Features that cannot be represented in 3.1 (e.g., Argon2 parameters)
are silently dropped during this export. The user is responsible for
understanding that the exported file is a lossy representation.

## Consequences

### Positive

- **Automatic version floor**: contributors adding a new KDBX 4.1
  feature only need to add a condition in `GetMinKdbxVersion` and the
  version bumps automatically when that feature is used.
- **No gratuitous upgrades**: a vault that happens to have been opened
  in a newer KeePass version but has no new features remains at its
  current version, preventing surprise compatibility breaks.
- **Deterministic**: the version is a pure function of vault content,
  making it easy to reason about and test with golden-file fixtures.

### Negative

- **Silent KDBX 4.1 upgrades**: users who add group tags or disable
  Quality Check on an entry will silently produce a 4.1 file on next
  save. There is no warning dialog. Older clients will refuse to open
  it.
- **No version downgrade path in the main save flow**: to produce a 3.1
  file, the user must explicitly export via `KeePassKdbx2v3`, which is
  lossy (drops custom data, downgrades cipher/KDF).
- **Content-first design couples feature addition to version bump**:
  adding a new 4.1 (or future 5.x) feature requires updating
  `GetMinKdbxVersion` as well as the serializer; forgetting either
  produces a corrupt or mis-versioned file.

### Neutral

- KDBX 3.1 is never the output of the normal save path (the floor is
  KDBX 4.0). Users who need 3.1 must use the explicit export format.
- The critical mask ensures that a KDBX 5.x reader will refuse to open
  a 4.x file, preserving forward-compatibility promises in both
  directions.

## Decision Table

| Content feature present | Minimum KDBX version returned |
|-------------------------|-------------------------------|
| None of the below | 4.0 |
| Any group has `Tags` | 4.1 |
| Any entry has `QualityCheck = false` | 4.1 |
| Any custom icon has `Name` or `LastModificationTime` | 4.1 |
| Any DB-level `CustomData` key has `LastModificationTime` | 4.1 |
| `ForceVersion` override set | `ForceVersion` value |

## Edge Cases

1. **Removing all 4.1 features**: if a user removes all group tags and
   re-enables QualityCheck on all entries, the minimum version returned
   by `GetMinKdbxVersion` drops back to 4.0. The file will be written as
   KDBX 4.0 on the next save. This is intentional and correct.

2. **KeePassKdbx2v3 with Argon2 active**: if the database uses Argon2
   KDF, the exporter silently replaces it with AES-KDF and discards the
   Argon2 tuning parameters. The export completes without error, but the
   security properties of the key derivation are reduced. No user prompt
   is shown.

3. **Inner vs outer header precedence (KDBX ≥ 4)**: KDBX 4 moved the
   cipher ID, compression flags, and inner-random-stream settings from
   the outer header into the inner header (protected by the master key).
   The reader resolves cipher/compression from inner-header fields when
   `FileVersion >= 4.0`; outer-header values are ignored for these fields
   even if present (for compatibility with files written by buggy tools).

## References

- `KeePassLib/Serialization/KdbxFile.cs` — `GetMinKdbxVersion()` method
  and version constants (`FileVersion32_4_1`, `FileVersion32_4`,
  `FileVersion32_3_1`, `FileVersionCriticalMask`)
- `KeePass/DataExchange/Formats/KeePassKdbx2.cs` — `KeePassKdbx2v3.Export`
- [ADR-000](ADR-000-template.md) — ADR template
- [KDBX format specification (keepass.info)](https://keepass.info/help/kb/kdbx.html)
