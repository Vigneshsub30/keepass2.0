# ADR-003: Plugin Trust Model and Isolation

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

KeePass plugins load arbitrary third-party code into the same process
that holds decrypted vault secrets. The current (KeePass 2.x) trust
model has significant gaps that make it unsuitable for a .NET 10 port.

### DLL Plugin Loading Path

DLL plugins are discovered by `PluginManager.LoadAllPlugins`
(`KeePass/Plugins/PluginManager.cs`). Each DLL is loaded via
`Activator.CreateInstanceFrom` with **no signature verification**. The
loader finds the first type that is a non-abstract subclass of
`KeePassLib.Plugins.Plugin` and instantiates it. There is no allow-list,
no publisher check, and no out-of-process boundary.

The only gate that exists is `CheckCompatibility`, which validates
`TypeRef` and `MemberRef` metadata tokens in the plugin assembly against
the KeePass assembly's public surface. This is a **compatibility check**,
not a security boundary — it detects API mismatches, not malicious code.

**Critically, `CheckCompatibility` is skipped on non-Windows platforms**
(`NativeLib.IsUnix()` guard). A plugin that would fail the token check on
Windows is silently admitted on macOS and Linux.

### PLGX Plugin Loading Path

PLGX plugins are a composite binary format handled by
`KeePass/Plugins/PlgxPlugin.cs`. The loading sequence is:

1. **PlgxSignature validation** — the file header must match two magic
   constants (`PlgxSignature1 = 0x65D90719`, `PlgxSignature2 = 0x3DDD0503`).
   This is an integrity marker, not a cryptographic signature.
2. **Prerequisite checks** — `PlgxPrerequisites` validates minimum KeePass
   version, minimum .NET version, OS platform string (`Windows`, `Unix`),
   and pointer size. The OS check can only express `Windows` or `Unix` —
   macOS is indistinguishable from Linux.
3. **On-demand C# compilation** — the PLGX container embeds C# source files
   that are compiled at startup via `CSharpCodeProvider`
   (`System.CodeDom.Compiler`). **`CSharpCodeProvider` does not exist on
   .NET 10.** This is a hard breaking change.
4. **PlgxCache** — compiled assemblies are stored on disk keyed by a
   SHA-256 hash of the PLGX container. On subsequent loads, the cached
   DLL is used directly. **No integrity re-verification of the cached DLL
   occurs.** The cache is a documented persistence vector — an attacker
   with file-system write access can replace a cached assembly between
   runs (Quarkslab analysis).
5. **LoadPlugin** — the compiled DLL is loaded via the same `LoadPlugin`
   path as a native DLL, inheriting all of the DLL trust-model gaps above.

### Trust-Model Gaps Summary

| Gap | Impact |
|-----|--------|
| No DLL signature verification | Any DLL on `%APPDATA%\KeePass\Plugins` runs with full trust |
| `CheckCompatibility` skipped on Unix | Cross-platform protection asymmetry |
| PlgxSignature is not a cryptographic signature | File is not attributed to a publisher |
| PLGX cache has no re-verification | Cached DLL can be silently replaced |
| All plugins run in-process, full trust | A malicious plugin reads all decrypted entries |
| `CSharpCodeProvider` removed in .NET 10 | PLGX compilation is broken unconditionally |

## Decision

We adopt a layered trust model for the .NET 10 port:

### Layer 1 — Pre-execution MetadataLoadContext Inspection

Before any plugin code executes, `PluginMetadataInspector`
(`KeePassLib/Plugins/PluginMetadataInspector.cs`) loads the assembly
metadata read-only via `System.Reflection.MetadataLoadContext`. The
inspector verifies:
- The assembly declares exactly one concrete `Plugin` subclass.
- The assembly references no blocked assemblies (e.g., `System.Windows.Forms`
  on non-Windows targets).
- The `TargetFrameworkAttribute` is present and compatible.

No plugin code executes during this phase.

### Layer 2 — Publisher-Signature Allow-List

`PluginSignatureVerifier` (`KeePassLib/Plugins/PluginSignatureVerifier.cs`)
verifies the plugin against a configurable allow-list of trusted publisher
public-key tokens (`AceSecurity.TrustedPluginPublishers`). On Windows,
Authenticode signatures are also checked. An empty allow-list means no
publisher restriction (default: permit all signed/unsigned plugins,
preserving current backward-compatibility). Administrators can lock the
allow-list via `AceSecurity.LockPluginPublisherAllowList` through the
enforced configuration file.

### Layer 3 — Collectible AssemblyLoadContext Isolation

Each admitted plugin is loaded into its own collectible
`AssemblyLoadContext` (`KeePass/Plugins/PluginLoadContext.cs`). This:
- Prevents plugin assembly references from leaking into the host domain.
- Allows a plugin to be unloaded and garbage-collected when disabled.
- Resolves dependency version conflicts between plugins.

### Layer 4 — PLGX Deprecation with Roslyn Shim

PLGX support is retained for a **12-month compatibility window** via a
Roslyn-backed shim (`KeePass/Plugins/RoslynPlgxCompiler.cs`) that
replaces `CSharpCodeProvider` with `Microsoft.CodeAnalysis.CSharp`. After
the 12-month window, PLGX loading will be disabled. Plugin authors must
migrate to signed DLLs targeting the `IPluginHost` abstraction.

### Optional Layer 5 — Out-of-Process gRPC Hosting (Future)

Untrusted plugins (those not on the publisher allow-list) may in future
be hosted in a child process communicating over local gRPC. This layer
is not implemented in the current modernization scope but is documented
here so architectural decisions that precede it (e.g., `IPluginHost`
surface area) do not foreclose it.

### Cross-Platform Token Validation Parity

The `CheckCompatibility` Unix skip (`NativeLib.IsUnix()` guard) has been
removed (`KeePass/Plugins/PluginManager.cs`, line 574-577). The
MetadataLoadContext-based reference check now runs on all platforms.

## Consequences

### Positive

- **Explicit trust decisions**: every plugin now passes an inspection gate
  before any code executes. Administrators can enforce a publisher allow-list
  via the enforced configuration.
- **Unloadability**: collectible ALCs allow disabled plugins to be fully
  unloaded, improving memory characteristics and enabling hot-reload in
  development.
- **PLGX migration path**: the Roslyn shim preserves PLGX compatibility
  for the 12-month window while giving authors a clear migration target.
- **Cross-platform parity**: the token check runs on all platforms,
  eliminating the Windows-only protection asymmetry.

### Negative

- **Ecosystem migration cost**: plugin authors must ship signed DLLs and
  opt into the ALC model. Existing PLGX plugins require source-level
  changes to compile with Roslyn if they use `CodeDom`-specific APIs.
- **No hard sandbox yet**: Layers 1-4 reduce the attack surface but do
  not eliminate in-process trust. A plugin still has access to the same
  process memory as the host. Layer 5 (out-of-process gRPC) is needed
  for a hard sandbox.
- **PlgxCache persistence vector remains**: the cache re-verification gap
  is not fully closed in the 12-month compatibility window. The Roslyn
  shim recompiles from source on hash mismatch, but the cached DLL can
  still be tampered with between runs.

### Neutral

- `IPluginHost` no longer exposes `MainForm` directly; plugins interact
  with the host via an abstraction (`KeePass.Core.Plugins.IPluginHost`).
  This is a breaking API change for all existing DLL plugins.
- The `CheckCompatibility` method is retained as a compatibility gate but
  is re-classified in comments as a compatibility check, not a security
  boundary.

## Edge Cases

1. **PLGX plugin references a non-public KeePass type on Unix**: Prior
   to this ADR, `CheckCompatibility` was skipped on Unix, so such a
   plugin was admitted. The MetadataLoadContext inspection in Layer 1
   now catches this on all platforms (it validates referenced type
   visibility).

2. **Tampered PlgxCache DLL**: Because the cache is keyed by the SHA-256
   hash of the PLGX container (not the output DLL), an attacker with
   file-system write access can replace the cached DLL. Mitigation during
   the compatibility window: the Roslyn shim records the SHA-256 of the
   compiled output alongside the cache entry; if the hashes disagree, the
   PLGX is recompiled from source. Full mitigation requires Layer 5.

3. **PLGX platform string `Unix` covers both macOS and Linux**: The PLGX
   prerequisite check cannot distinguish macOS from Linux — both match
   the `Unix` platform string. This is a known limitation of the PLGX
   format that is not addressable without a format change. Authors
   targeting macOS-specific APIs must use a conditional compilation
   symbol inside the embedded C# source.

## References

- `KeePass/Plugins/PluginManager.cs` — `LoadAllPlugins`, `LoadPlugin`,
  `CheckCompatibility`, `CheckCompatibilityPriv`
- `KeePass/Plugins/PlgxPlugin.cs` — `Load`, `ReadFile`, `ExtractFile`,
  `Compile`
- `KeePass/Plugins/PlgxCache.cs` — `GetCacheFile`, `AddCacheAssembly`,
  `GetCacheDirectory`
- `KeePassLib/Plugins/PluginMetadataInspector.cs` — pre-execution inspection
- `KeePassLib/Plugins/PluginSignatureVerifier.cs` — publisher-signature gate
- `KeePass/Plugins/PluginLoadContext.cs` — collectible ALC per plugin
- `KeePass/Plugins/RoslynPlgxCompiler.cs` — Roslyn PLGX compilation shim
- `KeePass/App/Configuration/AceSecurity.cs` — `TrustedPluginPublishers`,
  `LockPluginPublisherAllowList`
- [ADR-002](ADR-002-kdbx-format-version-selection.md) — KDBX format-version selection
- [ADR-000](ADR-000-template.md) — ADR template
- Quarkslab KeePass security analysis (PluginCache persistence vector)
