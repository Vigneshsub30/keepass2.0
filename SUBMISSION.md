# KeePass 2.0 — Cross-Platform Password Manager

**Forge Hackathon Submission**

---


| Field             | Details                                                                          |
| ----------------- | -------------------------------------------------------------------------------- |
| **Project**       | KeePass 2.0 — Cross-Platform Password Manager with Browser Integration           |
| **Hackathon**     | Forge Hackathon 2026                                                             |
| **Date**          | August 11, 2026                                                                  |
| **Team**          | Shailesh Kolap, Vignesh Subramanian                                              |
| **Repository**    | [github.com/Vigneshsub30/keepass2.0](https://github.com/Vigneshsub30/keepass2.0) |
| **Forge Project** | [SoftwareForge.ai Project](https://softwareforge.ai)                             |
| **Demo Video**    | *(Google Drive link — to be added)*                                              |


---



## Problem Statement

KeePass is a widely trusted, open-source password manager — but it has been locked to Windows and WinForms for over two decades. macOS users have had no native option, and there is no browser integration for auto-filling credentials. The codebase also had security gaps that needed addressing.

**Our goal:** Transform KeePass into a modern, cross-platform password manager with native macOS support and seamless browser integration — all in a single hackathon session, powered by AI-assisted development through Cursor and SoftwareForge.ai.

---



## What We Built



### Before (Original KeePass)

- Windows-only WinForms application
- No macOS or Linux GUI support
- No browser extension integration
- Security issues: global TLS bypass, unsigned plugins, no vault integrity checks



### After (Our Hackathon Work)

- **Cross-platform desktop app** built with Avalonia UI (MVVM architecture), supporting macOS, Linux, and Windows (end-to-end tested on macOS ARM64)
- **Browser extension integration** via the KeePassXC-Browser protocol
- **Full credential management**: create, edit, delete, search entries
- **Password generator** with configurable strength options
- **Import/Export** support for KDBX and CSV formats
- **Custom app icon** and polished DMG installer
- **Security hardening** across the entire codebase

---



## Architecture

```
┌─────────────────┐     Native Messaging      ┌───────────────────┐
│  Chrome/Firefox  │◄──────────────────────────►│   keepass-proxy   │
│  (KeePassXC-     │   stdin/stdout (JSON)      │  (Console App)    │
│   Browser ext.)  │                            └────────┬──────────┘
└─────────────────┘                                      │
                                                Unix Domain Socket
                                                         │
                                              ┌──────────▼──────────┐
                                              │  KeePass Avalonia   │
                                              │  Desktop App        │
                                              │  ┌────────────────┐ │
                                              │  │ BrowserSocket  │ │
                                              │  │ Server         │ │
                                              │  ├────────────────┤ │
                                              │  │ NaCl Crypto    │ │
                                              │  │ (X25519 +      │ │
                                              │  │  XSalsa20-     │ │
                                              │  │  Poly1305)     │ │
                                              │  ├────────────────┤ │
                                              │  │ Entry Manager  │ │
                                              │  │ Password Gen   │ │
                                              │  │ Import/Export  │ │
                                              │  └────────────────┘ │
                                              └──────────┬──────────┘
                                                         │
                                              ┌──────────▼──────────┐
                                              │   KeePassLib        │
                                              │   (.kdbx database)  │
                                              └─────────────────────┘
```

---



## Key Technical Achievements



### 1. Cross-Platform UI with Avalonia (MVVM)

Replaced the Windows-only WinForms frontend with a cross-platform Avalonia-based desktop app following the MVVM pattern. The app supports macOS, Linux, and Windows from a single codebase. End-to-end tested on macOS ARM64.

### 2. Browser Extension Integration (KeePassXC-Browser Protocol)

Implemented the full KeePassXC-Browser native messaging protocol, enabling the existing KeePassXC-Browser Chrome/Firefox extension to communicate with our app. This includes client association, database locking/unlocking, and credential retrieval.

### 3. End-to-End Encrypted Communication (NaCl)

All browser-to-app communication is encrypted using NaCl's `crypto_box` (X25519 key exchange + XSalsa20-Poly1305 authenticated encryption). A lightweight native messaging proxy bridges Chrome's stdin/stdout protocol with the app's Unix domain socket server.

### 4. Full Credential Lifecycle

Complete CRUD operations for password entries: create, read, update, delete, and search. Includes a configurable password generator and import/export support for KDBX and CSV formats.

### 5. Security Hardening

Addressed critical security gaps in the original codebase:

- Removed a global TLS certificate validation bypass
- Added plugin signature verification using MetadataLoadContext
- Implemented post-commit vault integrity checks with rollback runbooks
- Added a security review sign-off gate for release promotion

---



## Development Stats


| Metric                            | Value                      |
| --------------------------------- | -------------------------- |
| **Commits**                       | 30                         |
| **Files changed**                 | 62 (C#/XAML/project files) |
| **Lines of code added**           | ~5,000                     |
| **Lines of code modified**        | ~160                       |
| **Forge work orders completed**   | 15                         |
| **Architecture Decision Records** | 8 ADRs                     |
| **Development time**              | Single day                 |
| **AI-assisted development**       | Cursor + SoftwareForge.ai  |


---



## Forge Journey

This project was managed entirely through **SoftwareForge.ai**, which orchestrated the development workflow:

1. **Work Order Generation** — Forge analyzed the KeePass codebase and generated targeted work orders covering security fixes, architectural decisions, and feature development.
2. **ADR-Driven Architecture** — 8 Architecture Decision Records were produced to document key design choices (KDBX format selection, plugin trust model, clipboard credential delivery, MVVM adoption, and more).
3. **Security-First Approach** — Forge prioritized security work orders: TLS fix, plugin verification, vault integrity, and release promotion gates.
4. **Feature Development** — After the foundation was solid, Forge guided feature work: cross-platform UI, browser integration, entry management, and packaging.
5. **Continuous Validation** — Each work order included acceptance criteria verified through testing and validation.

---



## How to Run



### macOS (DMG)

1. Download `KeePass-2.61.1-osx-arm64.dmg` from the artifacts
2. Double-click to mount the disk image
3. Drag **KeePass Password Safe** to your Applications folder
4. Launch from Applications (right-click > Open on first launch to bypass Gatekeeper)
5. Open or create a `.kdbx` database file



### Browser Extension

1. Install the [KeePassXC-Browser extension](https://chromewebstore.google.com/detail/keepassxc-browser/oboonakemofpalcgghocfoadofidjkkk) for Chrome
2. With the KeePass app running and a database open, click the extension icon
3. Click **Connect** to pair the extension with the app
4. Navigate to any login page — the extension will offer to auto-fill credentials

---



## Commit Log


| Commit    | Description                                                                                     |
| --------- | ----------------------------------------------------------------------------------------------- |
| `9fef109` | Add app icon support in DMG packaging                                                           |
| `4f0edfb` | Update tests, solution and DMG packaging for new services                                       |
| `dc1e6b6` | Wire up desktop UI: entry editor, preview panel, import/export, browser integration, clean quit |
| `b0d2d4e` | Add native messaging proxy for browser extension communication                                  |
| `cd16f90` | Add core services: import/export, entry management, preview panel, browser protocol             |
| `122144d` | Add artifacts/ to .gitignore                                                                    |
| `48443b0` | Fix DMG creation for large single-file binaries                                                 |
| `7cd2036` | Fix DMG build: skip code signing when no certificate available                                  |
| `5680c48` | Enable dual-publish: Avalonia on macOS/Linux, WinForms on Windows                               |
| `80c7092` | Fix Windows test hang: downgrade xunit runner, add step timeouts                                |
| `7b221a2` | Fix CI: runsettings XML comment, CRLF shell scripts, skip packaging                             |
| `7353a0b` | Fix remaining CI failures: osx publish, Ubuntu platform tests                                   |
| `23e8a38` | Fix CI failures: bad action tag and Roslyn native-DLL filter                                    |
| `c81689b` | Fix YAML syntax errors in three workflow files                                                  |
| `9e910f0` | Fix pre-existing build failures blocking CI                                                     |
| `ac4dc83` | WO-105: Security review sign-off gate for release promotion                                     |
| `77c53bd` | WO-104: Cross-platform smoke test suite in CI                                                   |
| `adfea5c` | WO-103: Post-commit vault integrity check with rollback runbooks                                |
| `a8fffee` | WO-102: Beta release channel with automated promotion                                           |
| `cd30be3` | WO-101: Add packaging scripts and fix Build/ path casing                                        |
| `06444df` | WO-101: CI artifact matrix for three-platform signed builds                                     |
| `6b6b219` | WO-100: ADR for platform capability-tier matrix definition                                      |
| `3fa4233` | WO-099: ADR for atomic-save transaction guarantees                                              |
| `90a21a7` | WO-098: ADR for clipboard credential delivery mechanism                                         |
| `f08e74e` | WO-097: ADR for UI-domain seam and MVVM adoption                                                |
| `6128d74` | WO-096: ADR for Mono workaround retirement strategy                                             |
| `f5235cb` | WO-095: ADR for configuration enforcement precedence hierarchy                                  |
| `f4b8f41` | WO-094: ADR for plugin trust model and isolation                                                |
| `42d66ba` | WO-093: ADR for KDBX format-version selection logic                                             |
| `4b93389` | WO-091: Plugin signature verification gate with MetadataLoadContext                             |


---



## Technology Stack


| Layer           | Technology                               |
| --------------- | ---------------------------------------- |
| **Language**    | C# / .NET 8                              |
| **Desktop UI**  | Avalonia UI (MVVM, cross-platform)       |
| **Database**    | KeePassLib (KDBX 4.x format)             |
| **Crypto**      | NaCl.Net (X25519 + XSalsa20-Poly1305)    |
| **Browser IPC** | Native Messaging Protocol (stdin/stdout) |
| **App IPC**     | Unix Domain Sockets                      |
| **Packaging**   | DMG (macOS), self-contained publish      |
| **AI Tools**    | Cursor IDE, SoftwareForge.ai             |


---

*Built with Cursor and SoftwareForge.ai during the Forge Hackathon 2026 — August 11, 2026.*