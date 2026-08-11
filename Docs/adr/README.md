# Architectural Decision Records

This directory contains Architectural Decision Records (ADRs) for the KeePass
modernization project. An ADR captures a significant design choice, the context
that drove it, and its consequences — creating a durable, reviewable record of
*why* the codebase looks the way it does.

ADRs follow [Michael Nygard's format](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions).

## How to Create a New ADR

1. **Copy the template**
   ```
   cp docs/adr/ADR-000-template.md docs/adr/ADR-NNN-short-hyphenated-title.md
   ```
   Increment `NNN` from the last row in the index below.

2. **Fill in the sections** — replace every `<placeholder>` with real content.

3. **Set Status to `Proposed`** and open a pull request for team discussion.

4. **Update Status to `Accepted`** when the PR is merged, then add a row to the
   index table below.

## Status Values

| Value | Meaning |
|-------|---------|
| `Proposed` | Under discussion; not yet adopted |
| `Accepted` | Agreed upon and in effect |
| `Deprecated` | No longer applicable but kept for historical context |
| `Superseded` | Replaced by a later ADR (link provided in the record) |

## Index

| Number | Title | Status | Date |
|--------|-------|--------|------|
| [ADR-000](ADR-000-template.md) | ADR Template | Accepted | 2026-08-11 |
| [ADR-001](ADR-001-image-abstraction-breaking-change.md) | FileFormatProvider.SmallIcon return-type change from System.Drawing.Image to ImageData | Accepted | 2026-08-11 |
| [MonoWorkarounds-Classification](MonoWorkarounds-Classification.md) | MonoWorkarounds inventory and classification for .NET 10 migration (WO-043) | Accepted | 2026-08-11 |
