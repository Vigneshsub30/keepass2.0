<#
.SYNOPSIS
    Generates KeePass version-info manifest files for the stable and (optionally)
    beta update channels after a rollback.

.DESCRIPTION
    The KeePass in-app update check polls a signed version-info text file at a
    well-known URL.  After a rollback, the version-info files must be updated so
    that users on the bad release are immediately offered the rolled-back version.

    This script produces unsigned manifest stubs that a release manager must sign
    with the repository's RSA-4096 private key before publishing.  It writes:

      <OutputDir>/version2x.txt          — stable channel manifest
      <OutputDir>/version2x-beta.txt     — beta channel manifest (optional)

    The caller (typically the rollback.yml GitHub Actions workflow) then attaches
    these files to the rollback release as signing artefacts.

.PARAMETER Version
    The bare semantic version string to advertise (e.g. "2.61.1").
    Must be in Major.Minor.Patch format; the script derives the 64-bit
    file-version field from it.

.PARAMETER OutputDir
    Directory where the generated manifests are written.  Created if absent.

.PARAMETER IncludeBetaManifest
    When set, also writes a beta-channel manifest advertising the same version.
    Pass this flag when rolling back a release that was simultaneously present
    on both channels.

.EXAMPLE
    pwsh -File Build/rollback/update-version-info.ps1 `
         -Version "2.61.1" `
         -OutputDir "version-info-out" `
         -IncludeBetaManifest
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $OutputDir,

    [switch] $IncludeBetaManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function ConvertTo-FileVersion64 {
    param([string]$semver)
    # KeePass uses a packed 64-bit value: (Major << 48) | (Minor << 32) | (Patch << 16)
    [version]$v = $semver
    return ([long]$v.Major -shl 48) -bor ([long]$v.Minor -shl 32) -bor ([long]$v.Build -shl 16)
}

function New-VersionInfoContent {
    param(
        [string]$version,
        [long]$fileVersion64
    )

    # The version-info format used by KeePass UpdateCheckEx:
    #
    #   :NOSIG:          — placeholder stripped and replaced by the RSA signature
    #   KeePass:VER      — component name : file-version-string
    #   KeePassLib:VER
    #   :                — end sentinel
    #
    # The file-version string is the decimal representation of the 64-bit value
    # produced by PwDefs.FileVersion64 (the packed form).  UpdateCheckEx parses
    # this via StrUtil.ParseVersion / VersionUtil.CompareVersions.
    #
    # IMPORTANT: sign the output with the repository's RSA-4096 key and replace
    # ":NOSIG:" with the actual signature block before publishing.

    $lines = @(
        ":NOSIG:"
        "KeePass:$fileVersion64"
        "KeePassLib:$fileVersion64"
        ":"
    )
    return $lines -join "`n"
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-Verbose "Created output directory: $OutputDir"
}

$fv64 = ConvertTo-FileVersion64 -semver $Version
Write-Host "Version      : $Version"
Write-Host "FileVersion64: $fv64  (0x$("{0:X16}" -f $fv64))"

# Stable channel manifest
$stableContent = New-VersionInfoContent -version $Version -fileVersion64 $fv64
$stablePath    = Join-Path $OutputDir 'version2x.txt'
Set-Content -Path $stablePath -Value $stableContent -Encoding UTF8 -NoNewline
Write-Host "Written: $stablePath"

# Beta channel manifest (optional)
if ($IncludeBetaManifest) {
    $betaPath = Join-Path $OutputDir 'version2x-beta.txt'
    Set-Content -Path $betaPath -Value $stableContent -Encoding UTF8 -NoNewline
    Write-Host "Written: $betaPath"
}

Write-Host ""
Write-Host "IMPORTANT: The generated manifests contain ':NOSIG:' placeholders."
Write-Host "Sign them with the repository's RSA-4096 private key and publish"
Write-Host "them to the version-info hosting URLs before the rollback is complete."
