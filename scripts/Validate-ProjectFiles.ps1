<#
.SYNOPSIS
    Validates that every .cs file in a project directory is listed in the
    project's explicit Compile items, and that every Compile item exists on
    disk.

.DESCRIPTION
    Compares the <Compile Include="..."> entries in a .csproj file against the
    .cs files present under the project directory (excluding obj/ and bin/).
    Exits with code 1 if any .cs file on disk is absent from the csproj, so
    this script can be used as a CI gate.

.PARAMETER ProjectFile
    Absolute or relative path to the .csproj file to validate.

.PARAMETER ProjectDir
    Root directory to scan for .cs files. Defaults to the directory that
    contains ProjectFile.

.EXAMPLE
    pwsh -NoProfile -File scripts/Validate-ProjectFiles.ps1 `
         -ProjectFile KeePassLib/KeePassLib.csproj
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ProjectFile,

    [string]$ProjectDir = (Split-Path -Parent (Resolve-Path $ProjectFile))
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Normalise to absolute path.
$ProjectFile = Resolve-Path $ProjectFile
$ProjectDir  = $ProjectDir.TrimEnd([System.IO.Path]::DirectorySeparatorChar,
                                    [System.IO.Path]::AltDirectorySeparatorChar)

# ------------------------------------------------------------------
# Load the project XML and extract Compile Include paths.
# ------------------------------------------------------------------
[xml]$proj = Get-Content $ProjectFile -Raw

# Flatten across multiple ItemGroup elements; filter null/empty entries.
$inCsproj = @(
    $proj.Project.ItemGroup |
        ForEach-Object { $_.Compile } |
        Where-Object { $_ -and $_.Include } |
        ForEach-Object { $_.Include.Replace('\', '/') } |
        Sort-Object
)

# ------------------------------------------------------------------
# Enumerate .cs files on disk, skipping generated output directories.
# ------------------------------------------------------------------
$sep = [System.IO.Path]::DirectorySeparatorChar
$onDisk = @(
    Get-ChildItem -Path $ProjectDir -Filter '*.cs' -Recurse -Force |
        Where-Object {
            $_.FullName -notmatch ([regex]::Escape("${sep}obj${sep}")) -and
            $_.FullName -notmatch ([regex]::Escape("${sep}bin${sep}"))
        } |
        ForEach-Object {
            # Make the path relative to $ProjectDir and normalise separators.
            $_.FullName.Substring($ProjectDir.Length + 1).Replace('\', '/')
        } |
        Sort-Object
)

# ------------------------------------------------------------------
# Compute differences.
# ------------------------------------------------------------------
$untracked = @($onDisk  | Where-Object { $_ -notin $inCsproj })
$stale     = @($inCsproj | Where-Object { $_ -notin $onDisk  })

$projectName = Split-Path $ProjectFile -Leaf
$ok = $true

if ($untracked.Count -gt 0) {
    $ok = $false
    Write-Error ("${projectName}: ${$untracked.Count} file(s) on disk not listed in Compile items:`n" +
                 "  " + ($untracked -join "`n  "))
}

if ($stale.Count -gt 0) {
    Write-Warning ("${projectName}: $($stale.Count) Compile item(s) with no matching file on disk:`n" +
                   "  " + ($stale -join "`n  "))
}

if (-not $ok) { exit 1 }

Write-Host ("OK: ${projectName} — $($inCsproj.Count) Compile items match " +
            "$($onDisk.Count) .cs files on disk.")
