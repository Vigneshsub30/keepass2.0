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

## Downloads

Download from the [v1.0.0 release](https://github.com/Vigneshsub30/keepass2.0/releases/tag/v1.0.0):

| Platform | File |
|---|---|
| macOS (Apple Silicon) | `KeePass2.0-v1.0.0-osx-arm64.dmg` |
| macOS (Intel) | `KeePass2.0-v1.0.0-osx-x64.dmg` |
| Windows (x64) | `KeePass2.0-v1.0.0-win-x64.zip` |
| Linux (x64, .deb) | `KeePass2.0-v1.0.0-linux-x64.deb` |
| Linux (x64, tar.gz) | `KeePass2.0-v1.0.0-linux-x64.tar.gz` |

All builds are self-contained — no .NET runtime required.

## Quick Start

### macOS

1. Download the DMG for your Mac (ARM64 for Apple Silicon, x64 for Intel) from the [release page](https://github.com/Vigneshsub30/keepass2.0/releases/tag/v1.0.0)
2. Mount the disk image and drag **KeePass Password Safe** to Applications
3. Right-click > Open on first launch (to bypass Gatekeeper for unsigned builds)
4. Open or create a `.kdbx` database file

### Windows

1. Download `KeePass2.0-v1.0.0-win-x64.zip` from the [release page](https://github.com/Vigneshsub30/keepass2.0/releases/tag/v1.0.0)
2. Extract the ZIP to a folder
3. Run `KeePass.Desktop.Avalonia.exe`

### Linux

1. Download the `.deb` or `.tar.gz` from the [release page](https://github.com/Vigneshsub30/keepass2.0/releases/tag/v1.0.0)
2. For `.deb`: `sudo dpkg -i KeePass2.0-v1.0.0-linux-x64.deb`, then run `keepass`
3. For `.tar.gz`: extract and run `./KeePass.Desktop.Avalonia`

### Browser Extension

1. Install [KeePassXC-Browser](https://chromewebstore.google.com/detail/keepassxc-browser/oboonakemofpalcgghocfoadofidjkkk) for Chrome
2. With the KeePass app running and a database open, click the extension icon
3. Click **Connect** to pair the extension with the app
4. Navigate to any login page — credentials will auto-fill

### Build from Source

```bash
git clone https://github.com/Vigneshsub30/keepass2.0.git
cd keepass2.0
dotnet publish KeePass.Desktop.Avalonia -c Release -r osx-arm64 --self-contained
./artifacts/macos/publish/osx-arm64/KeePass.Desktop.Avalonia
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
| Packaging | DMG (macOS), ZIP (Windows), DEB/tar.gz (Linux) |

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
| Forge work orders | 15 |
| ADRs written | 8 |
| Development time | Single day |

See [SUBMISSION.md](SUBMISSION.md) for the full hackathon submission document.

---

## Team

- **Vignesh Subramanian**
- **Shailesh Kolap**

---

## License

Based on [KeePass](https://keepass.info/) by Dominik Reichl. See the original license terms for details.
