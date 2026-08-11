# ADR-004: Configuration Enforcement Precedence Hierarchy

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

KeePass has a four-tier configuration system. At runtime, a single
`AppConfigEx` object is produced by merging configuration from up to
four disk sources plus compiled defaults. The merge order, conflict
resolution rules, and the `AppPolicy` enforcement layer are implemented
in `AppConfigSerializer.cs`, `XmlUtil.Merge.cs`, and `AppPolicy.cs`
but are not documented outside those files.

### Configuration Sources and Paths

| Tier | File | Typical Location |
|------|------|-----------------|
| Enforced | `keepass.enforced.config.xml` | Next to the KeePass executable |
| Global | `KeePass.config.xml` | Next to the KeePass executable (portable) |
| User | `KeePass.config.xml` | `%APPDATA%\KeePass\` (installed) |
| Local override | path from `--config-path-local` CLI flag | Any writable path |

Paths are resolved in `AppConfigSerializer.GetConfigPaths()`.

### Complete Loading Sequence

`AppConfigSerializer.Load()` (`KeePass/App/Configuration/AppConfigSerializer.cs`,
method `Load()` at line 292) performs:

1. **Load enforced config** — `LoadEnforced(bSetPrimary: true)` reads
   `keepass.enforced.config.xml` and returns an `XmlDocument`.
   If the file does not exist, `xdEnforced` is `null`.

2. **Load global config** — `Load(g_strGlobalConfigFile, xdEnforced)`.
   After deserialisation, `XmlUtil.MergeElements` is called with the
   enforced `XmlDocument` as the overlay. Enforced nodes overwrite global
   nodes with the same path.

3. **Evaluate `PreferUserConfiguration` flag** — this boolean lives on
   `AppConfigEx.Meta`. If the global config has
   `PreferUserConfiguration = false`, the global config is returned
   immediately and the user config is **not read**.

4. **Load user config** — `Load(g_strUserConfigFile, xdEnforced)`.
   Again, `XmlUtil.MergeElements` applies the enforced overlay.

5. **Winner selection**:
   - Both `null` → create empty config and apply enforced overlay, or
     fall back to `new AppConfigEx()` with `OnLoad()` defaults.
   - Only global → return global.
   - Only user → return user.
   - Both present, `PreferUserConfiguration = true` → return user.
   - Both present, `PreferUserConfiguration = false` → return global.
   - `PreferUserConfiguration` on the returned config is always taken
     from the **global** config's value (line 337).

### Merge Semantics (`XmlUtil.MergeElements`)

The merge engine (`KeePass/Util/XmlUtil.Merge.cs`) processes each node
in the enforced overlay according to a `XmNodeOptions` object whose
properties are set by a per-path callback (`GetNodeOptions`):

| `XmNodeMode` | Effect on the target document |
|---|---|
| `OpenOrCreate` (default) | Merge enforced node into target; create if absent |
| `Create` | Add node only if absent; skip if already present |
| `Open` | Merge enforced node only if already present in target |
| `Remove` | **Delete the target node if present** — the key mechanism for enforced deletions |
| `None` | Skip this node entirely (no merge performed) |

**`XmNodeMode.Remove` is the primary enforcement mechanism.** An enforced
config node tagged `MergeNodeMode="Remove"` silently deletes the
corresponding user-config node. This allows administrators to suppress
user-configurable settings without replacing them.

A `GetNodeKey` callback provides a per-element identity key for list
merges (e.g., MRU entries, custom config entries). Without a key,
list elements are merged positionally; with a key they are matched by
value, enabling per-element remove/replace operations.

### AppPolicy Enforcement (17 Flags)

`AppPolicy.Current` (`KeePass/App/AppPolicy.cs`) holds 17 boolean flags
that gate runtime operations via `AppPolicy.Try(AppPolicyId)`. All flags
default to `true` (operation allowed). An enforced config that sets any
flag to `false` prevents the corresponding operation for all users.

| Flag | Default | Operation gated |
|------|---------|-----------------|
| `Plugins` | `true` | Loading and using plugins |
| `Export` | `true` | Exporting vault data |
| `ExportNoKey` | `true` | Exporting without requiring master key re-entry |
| `Import` | `true` | Importing data |
| `Print` | `true` | Printing vault data |
| `PrintNoKey` | `true` | Printing without master key re-entry |
| `NewFile` | `true` | Creating a new vault file |
| `SaveFile` | `true` | Saving the vault to disk |
| `AutoType` | `true` | Performing auto-type |
| `AutoTypeWithoutContext` | `true` | Auto-type without a matching window title |
| `CopyToClipboard` | `true` | Copying fields to the clipboard |
| `CopyWholeEntries` | `true` | Copying whole entries |
| `DragDrop` | `true` | Drag-and-drop operations |
| `UnhidePasswords` | `true` | Revealing masked passwords |
| `ChangeMasterKey` | `true` | Changing the vault master key |
| `ChangeMasterKeyNoKey` | `true` | Changing master key without re-entry |
| `EditTriggers` | `true` | Creating or modifying triggers |

Flags are stored in `AcePolicy` within `AppConfigEx.Security.Policy`.
`AppPolicy.ApplyToConfig` serialises the flags to the config object;
`AppPolicy.Try(id)` checks the current value.

### Enforced Config UAC Elevation

Writing to `keepass.enforced.config.xml` (next to the executable)
typically requires administrator privileges on Windows.
`AppEnforcedConfig.Modify(lItems, cfgValues, bAllowElevate)` in
`KeePass/App/Configuration/AppEnforcedConfig.cs` serialises the
modification, and if `bAllowElevate = true`, re-launches via
`WinUtil.RunElevated` to write the file. This path is only available
on Windows; macOS and Linux have no UAC equivalent.

## Decision

The precedence hierarchy is **enforced > selected-config > defaults**,
where "selected-config" is either global or user based on
`PreferUserConfiguration`. The complete ordered priority list is:

1. **Enforced config** (overlay applied to all other sources via
   `XmlUtil.MergeElements`). Enforced nodes win unconditionally.
   `XmNodeMode.Remove` can delete user/global nodes.
2. **User config** when `PreferUserConfiguration = true` in the global
   config (or when only the user config file exists).
3. **Global config** when `PreferUserConfiguration = false` (or when
   only the global config file exists).
4. **Local override** (`--config-path-local` replaces the normal user
   path for reads and writes; the enforced overlay is still applied).
5. **Compiled defaults** (`AppConfigEx.OnLoad()`) fill any property not
   present in any config source.

The `AppPolicy` flags impose a second enforcement layer orthogonal to
the XML merge: even if a setting is present in the user config, if the
corresponding policy flag is `false` in the resolved config, the
operation is blocked at runtime.

This precedence model is unchanged for the .NET 10 port. The
`XmlUtil.MergeElements` engine is retained as-is.

## Consequences

### Positive

- **Strong administrator control**: `XmNodeMode.Remove` allows enforced
  configs to delete user settings entirely, not just override them. An
  administrator can, for example, remove the MRU list, disable plugins,
  and lock clipboard operations via a single enforced config file.
- **Transparent merge**: the merge is XML-level, operating on the same
  serialised format used for user configs. An administrator can author
  an enforced config by copying and editing a user config.
- **Zero-privilege default path**: if the enforced config does not exist,
  the system falls through gracefully to global/user config without error.

### Negative

- **Silent suppression**: `XmNodeMode.Remove` deletes user nodes without
  any indication to the user that a setting has been suppressed. Users
  cannot distinguish "enforced off" from "not yet configured".
- **UAC-only enforcement write path on Windows**: writing an enforced
  config requires elevation on Windows and has no equivalent on
  macOS/Linux. Cross-platform administration requires a different
  deployment strategy (e.g., placing the file via MDM before KeePass runs).
- **`PreferUserConfiguration` coupling**: the flag that determines global
  vs user precedence lives inside the global config file. If the global
  config is corrupt or absent, user config is used unconditionally — there
  is no enforced-config fallback for this flag.

### Neutral

- `Local override` (`--config-path-local`) bypasses the normal user-config
  path for reads and writes, but the enforced overlay is still applied.
  It is primarily a developer/testing convenience and is not part of the
  normal deployment model.
- The `AppPolicy` flags are enforced separately from the XML merge and
  survive the merge unchanged. Administrators can combine XML-merge
  enforcement (to set specific values) with policy flags (to gate
  operations).

## Edge Cases

1. **`XmNodeMode.Remove` deletes a user setting silently**: if the
   enforced config marks a node with `MergeNodeMode="Remove"`, the
   corresponding user-config node is removed from the merged `XmlDocument`
   before deserialisation. The resulting `AppConfigEx` object has the
   compiled default for that field, not the user's value. The user sees no
   warning.

2. **`--config-path-local` with enforced config**: the local override
   replaces the user-config *path* (for both reads and writes). The
   enforced config is still loaded from the fixed path next to the
   executable and its overlay is applied to whatever the local path
   contains. The local-override config wins over the global config
   (because the load sequence loads it in the user-config slot).

3. **Per-collection merge identity (GetNodeKey)**: for list-typed fields
   (e.g., MRU entries in `AceMru`, custom config entries), the merge
   engine uses a `GetNodeKey` callback to compute an element identity key.
   Without this callback, list elements are merged by position — enforced
   additions append; enforced removals cannot target a specific element.
   With a `GetNodeKey` callback, individual list elements can be removed
   or replaced by key, allowing fine-grained per-entry enforcement.

## References

- `KeePass/App/Configuration/AppConfigSerializer.cs` — `Load()`,
  `LoadEnforced()`, `GetConfigPaths()`
- `KeePass/App/Configuration/AppEnforcedConfig.cs` — `Modify()`,
  UAC elevation via `WinUtil.RunElevated`
- `KeePass/App/AppPolicy.cs` — 17 `AppPolicyId` flags, `Try()`,
  `ApplyToConfig()`
- `KeePass/Util/XmlUtil.Merge.cs` — `XmNodeMode`, `XmNodeOptions`,
  `XmNodeOptionsDelegate`, `XmNodeKeyDelegate`
- `KeePass/App/Configuration/AppConfigEx.cs` — `OnLoad()` defaults,
  `Meta.PreferUserConfiguration`
- [ADR-000](ADR-000-template.md) — ADR template
