# ADR-001: FileFormatProvider.SmallIcon Return-Type Change from System.Drawing.Image to ImageData

- **Date:** 2026-08-11
- **Status:** Accepted

## Context

KeePass 2.x targets `net10.0-windows`. `System.Drawing.Common`, the library backing
`System.Drawing.Image`, throws `PlatformNotSupportedException` on non-Windows platforms
from .NET 7 onwards unless the `System.Drawing.EnableUnixSupport` app-context switch is
set — a switch that was removed in .NET 8.

To support future cross-platform UI heads (Avalonia, MAUI) and to allow the domain and
services layer (`KeePassLib`, `KeePass.Core`) to compile as platform-neutral `net10.0`
assemblies, all `System.Drawing` types must be removed from the shared API surface.

`FileFormatProvider` is the base class that all import/export format providers inherit
from, and its `SmallIcon` property was the most widely called `System.Drawing.Image`
property across the data-exchange subsystem. Changing its return type is the first step
in isolating `System.Drawing` to the WinForms head project.

## Decision

We will change the return type of `FileFormatProvider.SmallIcon` from
`System.Drawing.Image` to `KeePass.Core.Services.ImageData`, a platform-neutral value
type that carries encoded image bytes (PNG, ICO, BMP, JPEG) together with format and
dimension metadata.

A non-virtual compatibility adapter, `GetSmallIconAsImage()`, is added to the base class
so that existing WinForms UI code that needs a `System.Drawing.Image` can obtain one
without breaking the abstraction boundary:

```csharp
// WinForms caller — decodes ImageData back to System.Drawing.Image:
Image img = provider.GetSmallIconAsImage();

// Plugin override — wraps a resource image using the protected helper:
public override ImageData SmallIcon =>
    ImageDataFromResource(Properties.Resources.B16x16_MyFormat);
```

A protected static helper, `ImageDataFromResource(Image image)`, is added to
`FileFormatProvider` so that all existing subclasses can adapt to the new signature with
a one-line change and without requiring knowledge of PNG encoding.

## Consequences

### Positive

- `KeePass.Core.Services.IImageService` and `ImageData` contain no `System.Drawing`
  import — the abstraction compiles on Linux/macOS CI runners without runtime switches.
- All eight built-in `FileFormatProvider` subclasses have been updated; the pattern is
  clear and minimal for third-party plugin authors.
- The `GetSmallIconAsImage()` shim means no existing UI call sites need to understand
  `ImageData` encoding — they continue to receive a `System.Drawing.Image`.

### Negative

- **Breaking change for plugins.** Any third-party plugin that subclasses
  `FileFormatProvider` and overrides `SmallIcon` returning `System.Drawing.Image` will
  fail to compile after this change. Plugin authors must:
  1. Update the override return type from `System.Drawing.Image` to `ImageData`.
  2. Wrap the return value with `ImageDataFromResource(yourImage)`, or construct
     `ImageData` directly if they already hold raw bytes.
- `GetSmallIconAsImage()` performs a PNG encode + decode round-trip (via
  `Image.Save → MemoryStream → Image.FromStream`) on every call for formats backed by
  a live `System.Drawing.Image` resource. This is acceptable for infrequent UI
  population but callers should not call it in a tight loop.

### Neutral

- The `using System.Drawing;` import was removed from the eight format-provider files
  that only used it for the `SmallIcon` override. The `KeePass` project assembly still
  links against `System.Drawing.Common` via its Windows TFM.
- `KeePass.Core.csproj` now references `KeePassLib.csproj` so that `IImageService` can
  use the `PwIcon` enum for `GetStandardIcon`. This is a uni-directional dependency
  (`KeePass.Core` → `KeePassLib`); `KeePassLib` does not reference `KeePass.Core`.

## References

- [WO-027: Introduce IImageService Abstraction Replacing System.Drawing References]
- [WO-034: Decouple KeePassLib from WinForms] (next step in the same epic)
- [Microsoft — Breaking change: System.Drawing.Common only supported on Windows](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only)
