#!/usr/bin/env bash
# build/linux/build-appimage.sh
#
# Creates a portable AppImage from the dotnet publish layout using appimagetool.
#
# Usage:
#   build/linux/build-appimage.sh \
#       --version   2.61.1 \
#       --rid       linux-x64 \
#       --publish-dir ./artifacts/linux/publish/linux-x64 \
#       --output-dir  ./artifacts/linux
#
# Prerequisites on the runner (installed by the workflow):
#   - libfuse2 (or FUSE2-compatible) — apt-get install -y libfuse2
#   - appimagetool-x86_64.AppImage at /usr/local/bin/appimagetool
#     OR APPIMAGE_TOOL env var pointing to the binary
#
# Output: $OUTPUT_DIR/KeePass-$VERSION-$APPIMAGE_ARCH.AppImage
#
# AppDir layout:
#   AppDir/
#     AppRun               — launcher script
#     keepass2.desktop     — XDG desktop entry
#     keepass2.png         — icon (used by desktop environments)
#     usr/bin/KeePass.Desktop.Avalonia — the main executable
#     usr/lib/keepass2/    — all publish output

set -euo pipefail

# ── Argument parsing ──────────────────────────────────────────────────────────
VERSION=""
RID=""
PUBLISH_DIR=""
OUTPUT_DIR=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)    VERSION="$2";      shift 2 ;;
        --rid)        RID="$2";          shift 2 ;;
        --publish-dir) PUBLISH_DIR="$2"; shift 2 ;;
        --output-dir) OUTPUT_DIR="$2";   shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

if [[ -z "$VERSION" || -z "$RID" || -z "$PUBLISH_DIR" || -z "$OUTPUT_DIR" ]]; then
    echo "Usage: $0 --version <ver> --rid <rid> --publish-dir <dir> --output-dir <dir>" >&2
    exit 1
fi

if [[ ! -d "$PUBLISH_DIR" ]]; then
    echo "Publish directory not found: $PUBLISH_DIR" >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# ── Map RID to AppImage architecture string ───────────────────────────────────
case "$RID" in
    linux-x64)   APPIMAGE_ARCH="x86_64" ;;
    linux-arm64) APPIMAGE_ARCH="aarch64" ;;
    linux-arm)   APPIMAGE_ARCH="armhf" ;;
    *) echo "Unsupported RID for AppImage: $RID" >&2; exit 1 ;;
esac

# Locate appimagetool.
APPIMAGETOOL="${APPIMAGE_TOOL:-/usr/local/bin/appimagetool}"
if [[ ! -x "$APPIMAGETOOL" ]]; then
    echo "ERROR: appimagetool not found at $APPIMAGETOOL" >&2
    echo "       Download from https://github.com/AppImage/AppImageKit/releases" >&2
    exit 1
fi

# ── Assemble AppDir ───────────────────────────────────────────────────────────
APP_DIR="$(mktemp -d)/AppDir"
trap 'rm -rf "$(dirname "$APP_DIR")"' EXIT

mkdir -p "${APP_DIR}/usr/lib/keepass2" "${APP_DIR}/usr/bin"

# Copy publish output to usr/lib/keepass2.
cp -R "${PUBLISH_DIR}/." "${APP_DIR}/usr/lib/keepass2/"
chmod +x "${APP_DIR}/usr/lib/keepass2/KeePass.Desktop.Avalonia" 2>/dev/null || true

# Symlink the main executable so it appears in usr/bin (FHS convention).
ln -sf "../lib/keepass2/KeePass.Desktop.Avalonia" "${APP_DIR}/usr/bin/keepass2"

# AppRun — the entry point invoked by the AppImage runtime.
cat > "${APP_DIR}/AppRun" <<'APPRUN'
#!/bin/sh
SELF_DIR="$(dirname "$(readlink -f "$0")")"
exec "${SELF_DIR}/usr/lib/keepass2/KeePass.Desktop.Avalonia" "$@"
APPRUN
chmod 755 "${APP_DIR}/AppRun"

# .desktop entry (must be at AppDir root for appimagetool).
DESKTOP_SRC="${REPO_ROOT}/linux/org.keepass.desktop"
if [[ -f "$DESKTOP_SRC" ]]; then
    cp "$DESKTOP_SRC" "${APP_DIR}/keepass2.desktop"
else
    cat > "${APP_DIR}/keepass2.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=KeePass Password Safe 2
GenericName=Password Manager
Comment=Manage your passwords securely
Exec=keepass2 %F
Icon=keepass2
Categories=Utility;Security;
MimeType=application/x-keepass2;
Keywords=password;security;vault;kdbx;
StartupNotify=true
DESKTOP
fi

# Icon (must be at AppDir root as keepass2.png for appimagetool auto-detection).
ICON_SRC=$(find "$REPO_ROOT" -maxdepth 3 -name "keepass2.png" 2>/dev/null | head -1 || true)
if [[ -n "$ICON_SRC" ]]; then
    cp "$ICON_SRC" "${APP_DIR}/keepass2.png"
else
    # Placeholder 1×1 transparent PNG so appimagetool doesn't fail on missing icon.
    printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\x0bIDATx\x9cc\xf8\x0f\x00\x00\x01\x01\x00\x05\x18\xd8N\x00\x00\x00\x00IEND\xaeB`\x82' \
        > "${APP_DIR}/keepass2.png"
fi

# ── Build AppImage ─────────────────────────────────────────────────────────────
APPIMAGE_OUT="${OUTPUT_DIR}/KeePass-${VERSION}-${APPIMAGE_ARCH}.AppImage"
echo "==> Building AppImage: $APPIMAGE_OUT"

# appimagetool requires FUSE.  On CI runners without a real FUSE filesystem the
# tool can be invoked with APPIMAGETOOL_FUSE_EXTRACT=1 or in extract-and-run
# mode (the runner must have squashfs-tools available).
#
# Pass --no-appstream to skip AppStream validation (no appdata.xml present).
ARCH="$APPIMAGE_ARCH" "$APPIMAGETOOL" \
    --no-appstream \
    "$APP_DIR" \
    "$APPIMAGE_OUT"

chmod +x "$APPIMAGE_OUT"

# ── Verify ────────────────────────────────────────────────────────────────────
echo "==> Verifying AppImage …"
"$APPIMAGE_OUT" --appimage-extract-and-run --appimage-version 2>/dev/null \
    || echo "  (version extraction completed — exit code ignored)"

# Confirm key files are present in the squashfs.
"$APPIMAGE_OUT" --appimage-extract >/dev/null 2>&1
if [[ -f "squashfs-root/AppRun" ]]; then
    echo "  AppRun present: OK"
else
    echo "  ERROR: AppRun missing from AppImage" >&2; exit 1
fi
rm -rf squashfs-root

# ── Checksum ─────────────────────────────────────────────────────────────────
HASH=$(sha256sum "$APPIMAGE_OUT" | awk '{print $1}')
echo "AppImage SHA-256: $HASH  $APPIMAGE_OUT"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    echo "| AppImage ($RID) | \`$(basename "$APPIMAGE_OUT")\` | \`${HASH}\` |" >> "$GITHUB_STEP_SUMMARY"
fi

echo "==> AppImage packaging complete: $APPIMAGE_OUT"
