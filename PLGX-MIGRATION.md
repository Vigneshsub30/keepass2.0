# PLGX Plugin Migration Guide

## Background

KeePass historically supported `.plgx` (Plugin eXtended) packages — archives that contain
C# source code compiled at runtime by KeePass using `CSharpCodeProvider` (CodeDom).

As of the .NET 10 migration (WO-006), **runtime compilation via CodeDom is no longer
supported**. Attempting to load a `.plgx` plugin that requires runtime compilation will
throw a `PlatformNotSupportedException` with a descriptive message.

## What Still Works

- **Pre-compiled `.dll` plugins** continue to work without any changes. KeePass's
  existing cached-DLL loading path (`PlgxPlugin.cs`, `GetCachedAssembly`) is unaffected.
- Any `.plgx` plugin whose compiled assembly is already cached on disk from a previous
  .NET Framework KeePass run will load from the cache without recompilation.

## How to Migrate a `.plgx` Plugin to a `.dll`

1. **Obtain the plugin source.** Most open-source KeePass plugins are hosted on GitHub.
   Find the repository for the plugin you want.

2. **Build the plugin against .NET 10.**

   ```bash
   # Clone the plugin repo
   git clone https://github.com/<author>/<plugin>.git
   cd <plugin>

   # Build (adjust the target framework as needed)
   dotnet build -c Release -f net10.0-windows
   ```

3. **Locate the compiled assembly.** The output `.dll` (and its dependencies) will be in
   `bin/Release/net10.0-windows/` (or similar).

4. **Install the `.dll` as a KeePass plugin.** Copy the compiled `.dll` into KeePass's
   `Plugins` directory (typically the same folder as `KeePass.exe`). KeePass discovers
   plugin assemblies by file extension at startup.

5. **Verify the plugin loads.** Start KeePass and confirm the plugin appears under
   `Tools → Plugins`.

## Plugin Author Checklist

If you maintain a KeePass plugin distributed as `.plgx`, consider the following:

- Migrate the project to an SDK-style `.csproj` targeting `net10.0-windows` (or
  multi-targeting with `net10.0-windows;net48` for backward compatibility).
- Distribute a pre-compiled `.dll` release artifact via GitHub Releases / NuGet / etc.
- Optionally keep the `.plgx` for .NET Framework KeePass users and ship the `.dll` for
  .NET 10 KeePass users.

## Further Reading

- [KeePass Plugin Development](https://keepass.info/help/v2_dev/plg_index.html)
- [Migrating from CodeDom to Roslyn](https://learn.microsoft.com/en-us/dotnet/core/porting/)
- [SDK-style .csproj overview](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview)
