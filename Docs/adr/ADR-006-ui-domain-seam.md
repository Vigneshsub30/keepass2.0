# ADR-006: UI-Domain Seam and MVVM Adoption

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

The KeePass 2.x architecture has no separation between UI and domain logic.
Quantified coupling metrics from the original codebase:

| Metric | Value |
|--------|-------|
| `PwEntry` direct UI fan-in | 70 |
| `MessageService.ShowWarning` fan-in | 83 |
| Dependency balance (afferent / (afferent + efferent)) | 0.47 |
| `UIUtil.cs` line count | 1000+ |
| `System.Drawing` references in `UIUtil.cs` | ~2150 |

### UIUtil God-Class

`KeePass/UI/UIUtil.cs` is a static god-class mixing:

- **RTF formatting**: entry note rendering with inline color/font markup.
- **Image management**: `ImageList` population from embedded resources, icon
  cache management.
- **Entry list population**: `ListView` row construction directly from
  `PwEntry` instances.
- **Window state management**: `RestoreWindowState`, `SetWindowStyle`,
  font scaling.
- **P/Invoke marshaling**: Win32 `SendMessage`, `MONITORINFO` queries.
- **Utility formatting**: date/time, quality estimation, field masking.

There is no interface boundary between these responsibilities. Any UI head
(Avalonia, WinForms legacy, web) must link against the entire UIUtil surface.

### MainForm God-Object

`KeePass/Forms/MainForm.cs` has:

- **50+ private fields** tracking live UI state (entry list selection, drag
  state, sort state, document manager, session lock state, clipboard timer,
  auto-type target).
- **5 reentrant guards** (`m_uUIBlocked`, `m_uTabChangeBlocked`,
  `m_uBlockQuickFind`, `m_uBlockGroupSelectionEvent`,
  `m_uBlockEntrySelectionEvent`).
- **15 dynamically built menus** populated at runtime from plugin managers,
  trigger systems, and auto-type associations.

Business logic (filtering, sorting, clipboard timing, workspace locking) is
interleaved with WinForms event handlers, making extraction extremely risky
without a typed seam.

### Direct Domain-Type Consumption by UI

UI code consumes `PwEntry`, `PwGroup`, `PwDatabase`, `ProtectedString`, and
`ProtectedBinary` directly. There is no read-only projection or view model
between the domain and the presentation layer:

```
PwEntry (fan-in 70)
├── MainForm_Functions.cs (entry list row population)
├── UIUtil.cs (entry display formatting)
├── ClipboardUtil.cs (field copy)
├── EntryForm.cs (editor)
└── 66 other files
```

Mutating a displayed `PwEntry` field from UI code is structurally possible,
creating a correctness risk (UI thread ↔ background thread contention).

### Plugin Contract Coupling

`KeePassLib.Plugins.IPluginHost.MainWindow` returns `MainForm`
(a `System.Windows.Forms.Form` subtype). `Plugin.GetMenuItem()` returns
`System.Windows.Forms.ToolStripMenuItem`. This couples the plugin SDK to
the WinForms library, preventing plugin authors from targeting the Avalonia
head without shipping duplicate plugin assemblies.

## Decision

We adopt a layered architecture with an explicit UI–domain seam:

```
KeePassLib (domain)
    ↓ read-only projection DTOs
KeePass.Core (view models, services)
    ↓ INotifyPropertyChanged bindings
KeePass.Desktop.Avalonia / KeePass.Desktop.WinForms (platform heads)
```

### 1. Read-Only Projection DTOs

Two read-only record types in `KeePass.Core/Projections/` form the seam:

**`EntryProjection`** (`KeePass.Core/Projections/EntryProjection.cs`):

```csharp
public sealed record EntryProjection(
    PwUuid Uuid,
    string Title,
    string UserName,
    string Url,
    string Notes,
    IReadOnlyList<string> Tags,
    int IconIndex,
    PwUuid CustomIconUuid,
    DateTime? ExpiryTime,
    bool Expires,
    uint QualityBits,
    bool QualityCheck,
    IReadOnlyDictionary<string, string> CustomFields,
    IReadOnlyList<BinaryReference> Binaries,
    IReadOnlyList<EntryHistorySummary> HistorySummaries,
    IReadOnlyList<AutoTypeAssociation> AutoTypeAssociations,
    bool AutoTypeEnabled,
    string AutoTypeDefaultSequence,
    DateTime CreationTime,
    DateTime LastModificationTime
);
```

`ProtectedString` values are pre-resolved to plain strings at projection time.
The DTO is immutable — UI code cannot mutate domain state through it.

**`GroupProjection`** (`KeePass.Core/Projections/GroupProjection.cs`):

```csharp
public sealed record GroupProjection(
    PwUuid Uuid,
    string Name,
    string Notes,
    int IconIndex,
    PwUuid CustomIconUuid,
    bool IsExpanded,
    string DefaultAutoTypeSequence,
    InheritableBoolean EnableAutoType,
    InheritableBoolean EnableSearching,
    IReadOnlyList<GroupProjection> SubGroups,
    int TotalEntryCount
);
```

Mapper classes (`EntryProjectionMapper`, `GroupProjectionMapper`) in the same
namespace perform the `PwEntry` → `EntryProjection` and `PwGroup` →
`GroupProjection` translation. The mappers are the only location in
`KeePass.Core` allowed to reference `KeePassLib` domain types directly.

### 2. UIUtil Split Strategy

`UIUtil.cs` is split into three modules:

| Module | Responsibility | Layer |
|--------|---------------|-------|
| `KeePass.Core/UI/UIUtilCore.cs` | Platform-neutral formatting: date/time, quality estimation, field masking, password-strength string | `KeePass.Core` |
| `KeePass.Desktop.WinForms/UI/UIUtilWinForms.cs` | `System.Drawing`-dependent: `ImageList` management, `ListView` row population, RTF rendering | `KeePass.Desktop.WinForms` |
| `KeePass.Desktop.Avalonia/UI/UIUtilAvalonia.cs` | Avalonia-specific: `Bitmap` loading from embedded resources, icon rendering | `KeePass.Desktop.Avalonia` |

Call sites in `KeePass.Desktop.WinForms` reference `UIUtilWinForms` (no
change). New call sites in `KeePass.Desktop.Avalonia` reference
`UIUtilAvalonia`. Code that previously called `UIUtil.GetQualityString()` now
calls `UIUtilCore.GetQualityString()` from either head.

### 3. MVVM Adoption via CommunityToolkit.Mvvm

View models in `KeePass.Core/ViewModels/` consume `EntryProjection` and
`GroupProjection` via `CommunityToolkit.Mvvm`:

- `[ObservableObject]` base class provides `INotifyPropertyChanged`.
- `[ObservableProperty]` source-generates backing fields and setter
  change notification.
- `[RelayCommand]` source-generates `ICommand` implementations for
  user-initiated actions.

Key view models:

| View Model | Responsibility |
|------------|---------------|
| `MainWindowViewModel` | Database tab management, navigation, search |
| `EntryListItemViewModel` | Single row in the entry list (wraps `EntryProjection`) |
| `EntryEditorViewModel` | Full entry editor (wraps `PwEntry` at save time) |
| `GroupEditorViewModel` | Group editor |
| `KeyPromptViewModel` | Master-key entry |
| `PasswordGeneratorViewModel` | Password generation |
| `SearchViewModel` | Search filter UI |

View models are registered in the DI container (`KeePass.Core`) and are
platform-agnostic. Both the Avalonia and WinForms heads bind to the same view
models.

### 4. IPluginHost Abstraction

`KeePassLib.Plugins.IPluginHost` is refactored to remove WinForms types:

```csharp
// Before:
MainForm MainWindow { get; }

// After:
IMainWindowService MainWindow { get; }
```

`IMainWindowService` provides:
- `void ShowNotification(string message)` — cross-platform notification display.
- `void InvokeOnUiThread(Action action)` — thread marshalling without WinForms.
- `IntPtr WindowHandle { get; }` — raw handle for OS-level operations.

`PluginMenuCommand` DTO replaces `ToolStripMenuItem`:

```csharp
public sealed class PluginMenuCommand
{
    public string Text { get; init; }
    public byte[]? IconPngBytes { get; init; }
    public string? ShortcutKey { get; init; }
    public Action? Execute { get; init; }
    public Func<bool>? CanExecute { get; init; }
}
```

Both heads adapt `PluginMenuCommand` to their native menu item type.

## Consequences

### Positive

- **Second UI head becomes implementable**: an Avalonia view can bind to
  `EntryListItemViewModel` / `GroupTreeItemViewModel` without touching
  `KeePassLib` domain types. The WO-097 target metric — PwEntry direct UI
  fan-in ≤35 — is achievable through progressive migration.
- **Domain mutation from UI is prevented**: `EntryProjection` and
  `GroupProjection` are immutable records. UI code cannot accidentally modify
  vault state through a displayed entry's property.
- **View model unit tests**: view models take `EntryProjection` inputs
  (plain data) and return observable state. They can be tested without
  a running WinForms or Avalonia application.
- **Plugin authors target a stable, WinForms-free API**: `IMainWindowService`
  and `PluginMenuCommand` decouple plugins from `System.Windows.Forms`.

### Negative

- **Projection cost at render time**: every entry displayed in the list
  requires an `EntryProjection` allocation. For large vaults (100k+ entries),
  this is a measurable GC pressure increase. Mitigation: `EntryListItemViewModel`
  uses lazy population and only projects visible entries.
- **Protected-field decryption at projection time**: `ProtectedString`
  values are decrypted to `string` inside the mapper. If a projection is
  cached, the cleartext password lives in the managed heap until GC. The
  mapper does not cache projections for security-sensitive entries by default.
- **Migration is incremental, not big-bang**: the WinForms head retains
  direct `PwEntry` references in some paths during the migration window.
  Both patterns coexist until all call sites are migrated.

### Neutral

- `UIUtil.cs` is not deleted in a single commit; it is split incrementally
  as each call site is migrated to `UIUtilCore`, `UIUtilWinForms`, or
  `UIUtilAvalonia`.
- The WinForms `MainForm` class is not removed. It is retained as the host
  for the transitional desktop head (`KeePass.Desktop.WinForms`) and adapts
  `IMainWindowService` over the existing WinForms window.

## Edge Cases

1. **ProtectedString in projection DTOs**: `EntryProjection` holds
   `string Title`, `string UserName`, and `string Password` (resolved from
   `ProtectedString` at map time). The mapper clears the intermediate
   `SecureString` after the plain string is produced. The DTO itself holds
   a plain `string` — it must not be serialised to disk or transmitted over
   a network. Call sites that need to copy the password to clipboard do so
   through `IClipboardService`, not through the DTO.

2. **AsyncPwListUpdate background thread and MVVM binding**: the original
   entry list was populated by `AsyncPwListUpdate`, which mutated `ListView`
   items on a background thread with `Invoke()`. In the MVVM model,
   `MainWindowViewModel` exposes an `ObservableCollection<EntryListItemViewModel>`
   that is populated on the UI thread via `Dispatcher.InvokeAsync`. Background
   filtering is done against an immutable snapshot of `EntryProjection` records,
   then the UI collection is updated on the UI thread.

3. **DynamicMenu and PluginMenuCommand**: the original `Plugin.GetMenuItem()`
   returned a `ToolStripMenuItem` tree that was inserted into `MainForm`'s
   menu strip. With `PluginMenuCommand`, each head builds its own menu item:
   - WinForms head: `ToolStripMenuItem` from `PluginMenuCommand.Text`/`Execute`.
   - Avalonia head: `NativeMenuItem` from the same DTO.
   Dynamic sub-menus (populated just before display) are expressed as
   `PluginMenuCommand.ChildProvider: Func<IReadOnlyList<PluginMenuCommand>>`.

## References

- `KeePass.Core/Projections/EntryProjection.cs` — read-only entry DTO
- `KeePass.Core/Projections/GroupProjection.cs` — read-only group DTO
- `KeePass.Core/Projections/EntryProjectionMapper.cs` — mapper from `PwEntry`
- `KeePass.Core/Projections/GroupProjectionMapper.cs` — mapper from `PwGroup`
- `KeePass.Core/UI/UIUtilCore.cs` — platform-neutral formatting utilities
- `KeePass.Core/ViewModels/` — all MVVM view models
- `KeePassLib/Plugins/IPluginHost.cs` — plugin host abstraction
- `KeePass/Plugins/PluginMenuCommand.cs` — menu DTO
- [ADR-003](ADR-003-plugin-trust-model.md) — plugin trust model
- [ADR-000](ADR-000-template.md) — ADR template
- [CommunityToolkit.Mvvm documentation](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
