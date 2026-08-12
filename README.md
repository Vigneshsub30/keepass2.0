# KeePass 2.0 — Cross-Platform Password Manager

A modern, cross-platform fork of [KeePass](https://keepass.info/) built with .NET 8 and Avalonia UI. This project transforms the original Windows-only WinForms application into a fully functional password manager that runs on **macOS, Linux, and Windows** — with seamless **browser extension integration** via the KeePassXC-Browser protocol.

> Built during the **Forge Hackathon 2026** using [Cursor](https://cursor.com/) and [SoftwareForge.ai](https://softwareforge.ai/).

---

## Features

- **Cross-platform desktop app** — Avalonia UI with MVVM architecture, single codebase for macOS, Linux, and Windows
- **Browser extension integration** — Works with the [KeePassXC-Browser](https://chromewebstore.google.com/detail/keepassxc-browser/oboonakemofpalcgghocfoadofidjkkk) extension for Chrome and Firefox
- **End-to-end encrypted communication** — NaCl crypto_box (X25519 + XSalsa20-Poly1305) between browser and app
- **Full credential management** — Create, edit, delete, and search password entries
- **Password generator** — Configurable strength and character set options
- **Import/Export** — KDBX and CSV format support
- **Native macOS packaging** — DMG installer with custom app icon
- **Security hardening** — TLS fix, plugin signature verification, vault integrity checks

---

## Architecture

```
Chrome/Firefox            Native Messaging           keepass-proxy
(KeePassXC-Browser) <--- stdin/stdout (JSON) ---> (Console App)
                                                        |
                                               Unix Domain Socket
                                                        |
                                                        v
                                              KeePass Avalonia App
                                             +------------------+
                                             | BrowserSocket    |
                                             | Server           |
                                             +------------------+
                                             | NaCl Crypto      |
                                             +------------------+
                                             | Entry Manager    |
                                             | Password Gen     |
                                             | Import/Export    |
                                             +------------------+
                                                        |
                                               KeePassLib (.kdbx)
```

---

## Quick Start

### macOS (DMG)

1. Download `KeePass-2.61.1-osx-arm64.dmg` from the [latest release](https://github.com/Vigneshsub30/keepass2.0/releases)
2. Mount the disk image and drag **KeePass Password Safe** to Applications
3. Right-click > Open on first launch (to bypass Gatekeeper for unsigned builds)
4. Open or create a `.kdbx` database file

### Browser Extension

1. Install [KeePassXC-Browser](https://chromewebstore.google.com/detail/keepassxc-browser/oboonakemofpalcgghocfoadofidjkkk) for Chrome
2. With the KeePass app running and a database open, click the extension icon
3. Click **Connect** to pair the extension with the app
4. Navigate to any login page — credentials will auto-fill

### Build from Source

```bash
# Clone the repo
git clone https://github.com/Vigneshsub30/keepass2.0.git
cd keepass2.0

# Build and run (macOS)
dotnet publish KeePass.Desktop.Avalonia -c Release -r osx-arm64 --self-contained
./artifacts/macos/publish/osx-arm64/KeePass.Desktop.Avalonia

# Package as DMG
SKIP_SIGNING=true bash Build/macos/build-dmg.sh \
    --version 2.61.1 --rid osx-arm64 \
    --publish-dir ./artifacts/macos/publish/osx-arm64 \
    --output-dir ./artifacts/osx-arm64
```

---

## Technology Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 8 |
| Desktop UI | Avalonia UI (MVVM) |
| Database | KeePassLib (KDBX 4.x) |
| Crypto | NaCl.Net (X25519 + XSalsa20-Poly1305) |
| Browser IPC | Native Messaging Protocol |
| App IPC | Unix Domain Sockets |
| Packaging | DMG (macOS), self-contained publish |

---

## Project Structure

```
KeePass.Core/              Core view models, services, and interfaces
KeePass.Desktop.Avalonia/  Avalonia desktop app (macOS/Linux/Windows)
KeePass.Platform.Unix/     macOS/Linux platform services (clipboard, etc.)
KeePass.Proxy/             Native messaging proxy for browser communication
KeePassLib/                KeePass database library (KDBX read/write)
Build/macos/               DMG packaging scripts
Docs/adr/                  Architecture Decision Records
```

---

## Hackathon Stats

| Metric | Value |
|---|---|
| Commits | 30 |
| Files changed | 62 |
| Lines of code added | ~5,000 |
| Forge work orders | 15 |
| ADRs written | 8 |
| Development time | Single day |

See [SUBMISSION.md](SUBMISSION.md) for the full hackathon submission document.

---

## Team

- **Shailesh Kolap**
- **Vignesh Subramanian**

---

## License

Based on [KeePass](https://keepass.info/) by Dominik Reichl. See the original license terms for details.
