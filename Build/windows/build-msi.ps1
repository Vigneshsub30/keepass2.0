<#
.SYNOPSIS
  Packages the published KeePass output into an MSI installer using WiX Toolset v4.

.DESCRIPTION
  This script is invoked by the GitHub Actions build.yml workflow for the Windows
  packaging step.  It expects dotnet publish to have run first, producing a
  self-contained layout under the path given by -PublishDir.

  Prerequisites on the runner:
    - .NET 10 SDK (set up by actions/setup-dotnet)
    - WiX Toolset v4 installed as a .NET tool:
        dotnet tool install --global wix

  The output MSI is written to $OutputDir\KeePass-$Version-win-$Arch.msi.

.PARAMETER Version
  Version string in Major.Minor.Patch format (e.g. "2.61.1").

.PARAMETER Arch
  Runtime architecture: "x64" or "arm64".

.PARAMETER PublishDir
  Path to the dotnet publish output directory.

.PARAMETER OutputDir
  Directory where the MSI is written.  Created if absent.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [ValidateSet('x64', 'arm64')]
    [string]$Arch,

    [Parameter(Mandatory)]
    [string]$PublishDir,

    [Parameter(Mandatory)]
    [string]$OutputDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Validate inputs ──────────────────────────────────────────────────────────
if (-not (Test-Path $PublishDir)) {
    Write-Error "Publish directory not found: $PublishDir"
    exit 1
}

$mainExe = Join-Path $PublishDir 'KeePass.exe'
if (-not (Test-Path $mainExe)) {
    Write-Error "KeePass.exe not found in publish directory: $PublishDir"
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ── Derive WiX architecture and platform identifiers ─────────────────────────
$wixArch = if ($Arch -eq 'arm64') { 'arm64' } else { 'x64' }

# Upgrade code is architecture-specific so side-by-side x64 and arm64 installs
# are treated as distinct products by Windows Installer.
$upgradeCodeMap = @{
    'x64'   = 'A1B2C3D4-E5F6-7A8B-9C0D-E1F2A3B4C5D6'
    'arm64' = 'B2C3D4E5-F6A7-8B9C-0D1E-F2A3B4C5D6E7'
}
$upgradeCode = $upgradeCodeMap[$Arch]

# ── Write a minimal WiX 4 source file ─────────────────────────────────────────
$wxsPath = Join-Path $env:TEMP "keepass-msi.wxs"
$msiOut   = Join-Path $OutputDir "KeePass-${Version}-win-${Arch}.msi"

# Build a <File> element per file in the publish directory so all binaries are
# included.  File GUIDs are omitted so WiX 4 generates stable GUIDs from the
# file hash (default behaviour for HarvestFiles).
#
# We use a single HarvestDirectory element (wix extension Util) instead of
# enumerating files manually.  This guarantees the MSI captures every file
# that dotnet publish places in the layout without requiring per-file GUIDs.
$wxsContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">

  <Package Name="KeePass Password Safe" Version="$Version"
           Manufacturer="Dominik Reichl"
           UpgradeCode="$upgradeCode"
           InstallerVersion="500"
           Compressed="yes"
           Scope="perMachine">

    <MajorUpgrade DowngradeErrorMessage="A newer version of KeePass is already installed." />

    <MediaTemplate EmbedCab="yes" />

    <Feature Id="ProductFeature" Title="KeePass Password Safe" Level="1">
      <ComponentGroupRef Id="ProductComponents" />
      <ComponentRef Id="ProgramMenuShortcutComponent" />
    </Feature>

    <StandardDirectory Id="ProgramFiles6432Folder">
      <Directory Id="KeePassDir" Name="KeePass Password Safe 2">
        <util:HarvestDirectory Id="ProductComponents"
                               Directory="$($PublishDir.Replace('\','\\'))"
                               Subdirectory="." />
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="KeePass Password Safe 2">
        <Component Id="ProgramMenuShortcutComponent" Guid="*">
          <Shortcut Id="ApplicationStartMenuShortcut"
                    Name="KeePass Password Safe 2"
                    Description="Manage your passwords securely"
                    Target="[KeePassDir]KeePass.exe"
                    WorkingDirectory="KeePassDir" />
          <RemoveFolder Id="CleanUpShortCut" Directory="ApplicationProgramsFolder" On="uninstall" />
          <RegistryValue Root="HKCU"
                         Key="Software\KeePass\Installer"
                         Name="installed"
                         Type="integer"
                         Value="1"
                         KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>

    <Property Id="WIXUI_INSTALLDIR" Value="KeePassDir" />
    <UIRef Id="WixUI_InstallDir" />

  </Package>
</Wix>
"@

$wxsContent | Out-File -FilePath $wxsPath -Encoding utf8

Write-Host "Building MSI: $msiOut"
Write-Host "  Architecture : $wixArch"
Write-Host "  Version      : $Version"
Write-Host "  Source       : $PublishDir"

# ── Run WiX build ─────────────────────────────────────────────────────────────
wix build "$wxsPath" `
    -arch $wixArch `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Util.wixext `
    -out "$msiOut"

if ($LASTEXITCODE -ne 0) {
    Write-Error "WiX build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# ── Emit checksum ─────────────────────────────────────────────────────────────
$hash = (Get-FileHash $msiOut -Algorithm SHA256).Hash
Write-Host "MSI SHA-256: $hash  $msiOut"

# Append to GITHUB_STEP_SUMMARY when running in Actions.
if ($env:GITHUB_STEP_SUMMARY) {
    "| MSI ($Arch) | ``$msiOut`` | ``$hash`` |" | Add-Content $env:GITHUB_STEP_SUMMARY
}

Write-Host "MSI packaging complete."
