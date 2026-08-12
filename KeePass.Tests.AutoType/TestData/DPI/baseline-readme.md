# DPI Baseline Data

This directory contains golden baseline measurements generated from the
.NET Framework runtime on a Windows development machine.

## Machine Configuration
- OS: Windows 10 22H2
- Monitor: 1920×1080, 96 DPI (100%)
- .NET Framework version: 4.8.1
- KeePass version: 2.61.1

## How to Regenerate
1. Build KeePass in Debug mode targeting .NET Framework 4.8.
2. Run `KeePass.exe /measure-dpi` (baseline generation flag added in WO-085).
3. Copy the generated `dpi-baseline-*.json` files to this directory.
4. Commit the updated files.

## Scaling Formula
DpiUtil.ScaleIntX/ScaleIntY: `(int)Math.Round(value * (dpi / 96.0))`

Known scaling factors:
- 100% (96 DPI):  factor = 1.0
- 125% (120 DPI): factor ≈ 1.25
- 150% (144 DPI): factor = 1.5
- 200% (192 DPI): factor = 2.0
