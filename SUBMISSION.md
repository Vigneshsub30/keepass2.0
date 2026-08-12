# KeePass 2.0 — Cross-Platform Password Manager

**Forge Hackathon Submission**

---


| Field             | Details                                                                          |
| ----------------- | -------------------------------------------------------------------------------- |
| **Project**       | KeePass 2.0 — Cross-Platform Password Manager with Browser Integration           |
| **Hackathon**     | Forge Hackathon 2026                                                             |
| **Date**          | August 11, 2026                                                                  |
| **Team**          | Vignesh Subramanian, Shailesh Kolap                                              |
| **Repository**    | [github.com/Vigneshsub30/keepass2.0](https://github.com/Vigneshsub30/keepass2.0) |
| **Forge Project** | [Keepass Modernization Project](https://hackathon.softwareforge.ai/projects/32e99329-eb95-42b4-99f6-b1ff6f4ce5b8)                             |
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
| **Forge work orders completed**   | 105                        |
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



### Downloads

Download from the [v1.0.0 release](https://github.com/Vigneshsub30/keepass2.0/releases/tag/v1.0.0):

| Platform | File |
|---|---|
| macOS (Apple Silicon) | `KeePass2.0-v1.0.0-osx-arm64.dmg` |
| macOS (Intel) | `KeePass2.0-v1.0.0-osx-x64.dmg` |
| Windows (x64) | `KeePass2.0-v1.0.0-win-x64.zip` |
| Linux (x64, .deb) | `KeePass2.0-v1.0.0-linux-x64.deb` |
| Linux (x64, tar.gz) | `KeePass2.0-v1.0.0-linux-x64.tar.gz` |

### macOS (DMG)

1. Download the DMG for your Mac (ARM64 for Apple Silicon, x64 for Intel)
2. Double-click to mount the disk image
3. Drag **KeePass Password Safe** to your Applications folder
4. Launch from Applications (right-click > Open on first launch to bypass Gatekeeper)
5. Open or create a `.kdbx` database file

### Windows

1. Download `KeePass2.0-v1.0.0-win-x64.zip`
2. Extract the ZIP to a folder
3. Run `KeePass.Desktop.Avalonia.exe`

### Linux

1. Download the `.deb` or `.tar.gz` for your distribution
2. For `.deb`: `sudo dpkg -i KeePass2.0-v1.0.0-linux-x64.deb`, then run `keepass`
3. For `.tar.gz`: extract and run `./KeePass.Desktop.Avalonia`



### Browser Extension

1. Install the [KeePassXC-Browser extension](https://chromewebstore.google.com/detail/keepassxc-browser/oboonakemofpalcgghocfoadofidjkkk) for Chrome
2. With the KeePass app running and a database open, click the extension icon
3. Click **Connect** to pair the extension with the app
4. Navigate to any login page — the extension will offer to auto-fill credentials

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
| **Packaging**   | DMG (macOS), ZIP (Windows), DEB (Linux)  |
| **AI Tools**    | Cursor IDE, SoftwareForge.ai             |


---

*Built with Cursor and SoftwareForge.ai during the Forge Hackathon 2026 — August 11, 2026.*
