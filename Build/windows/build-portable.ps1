<#
.SYNOPSIS
  Creates a portable (no-install) ZIP archive from the published KeePass output.

.DESCRIPTION
  Invoked by the GitHub Actions build.yml workflow for the Windows portable
  artifact step.  Packages everything in the dotnet publish layout plus a
  README and LICENSE file into a single compressed ZIP.

  The output archive is written to:
    $OutputDir\KeePass-$Version-win-$Arch-portable.zip

.PARAMETER Version
  Version string in Major.Minor.Patch format (e.g. "2.61.1").

.PARAMETER Arch
  Runtime architecture: "x64" or "arm64".

.PARAMETER PublishDir
  Path to the dotnet publish output directory.

.PARAMETER OutputDir
  Directory where the ZIP is written.  Created if absent.

.PARAMETER RepoRoot
  Root of the repository checkout.  Used to locate README and LICENSE files.
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
    [string]$OutputDir,

    [Parameter()]
    [string]$RepoRoot = $PSScriptRoot
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

# ── Stage a clean temporary directory ─────────────────────────────────────────
$stagingDir = Join-Path $env:TEMP "KeePass-portable-staging"
if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

# Copy published output.
$destBin = Join-Path $stagingDir "KeePass-${Version}-win-${Arch}"
Copy-Item -Recurse -Force $PublishDir $destBin

# Copy supplementary files from the repo root if they exist.
$supplementary = @('README.md', 'LICENSE', 'LICENSE.txt', 'CHANGELOG.md')
foreach ($file in $supplementary) {
    $src = Join-Path $RepoRoot $file
    if (Test-Path $src) {
        Copy-Item -Force $src $destBin
        Write-Host "Added $file"
    }
}

# ── Compress ──────────────────────────────────────────────────────────────────
$zipOut = Join-Path $OutputDir "KeePass-${Version}-win-${Arch}-portable.zip"
Write-Host "Creating portable ZIP: $zipOut"

Compress-Archive -Path $destBin -DestinationPath $zipOut -CompressionLevel Optimal -Force

# ── Cleanup staging area ──────────────────────────────────────────────────────
Remove-Item -Recurse -Force $stagingDir

# ── Emit checksum ─────────────────────────────────────────────────────────────
$hash = (Get-FileHash $zipOut -Algorithm SHA256).Hash
Write-Host "ZIP SHA-256: $hash  $zipOut"

if ($env:GITHUB_STEP_SUMMARY) {
    "| Portable ZIP ($Arch) | ``$zipOut`` | ``$hash`` |" | Add-Content $env:GITHUB_STEP_SUMMARY
}

Write-Host "Portable ZIP packaging complete."
