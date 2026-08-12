#!/usr/bin/env bash
# package-appimage.sh — Build a universal AppImage for linux-x64.
#
# Usage:
#   ./linux/package-appimage.sh [<output_dir>]
#
# Dependencies: appimagetool (from https://github.com/AppImage/AppImageKit)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

OUTPUT_DIR="${1:-${REPO_ROOT}/artifacts/linux}"
VERSION="${KEEPASS_VERSION:-2.61.1}"

PUBLISH_DIR="${OUTPUT_DIR}/publish-linux-x64"
ICONS_DIR="${SCRIPT_DIR}/icons"
APPIMAGE_TOOL="${APPIMAGE_TOOL:-appimagetool}"

if [[ ! -d "${PUBLISH_DIR}" ]]; then
    echo "Error: ${PUBLISH_DIR} not found. Run build-linux.sh first." >&2
    exit 1
fi

echo "==> Creating AppImage (x86_64, v${VERSION})…"

APPDIR="$(mktemp -d)/KeePass.AppDir"
mkdir -p "${APPDIR}/usr/bin" "${APPDIR}/usr/lib/keepass"

cp -R "${PUBLISH_DIR}/." "${APPDIR}/usr/lib/keepass/"
chmod +x "${APPDIR}/usr/lib/keepass/KeePass.Desktop.Avalonia"

# AppRun entry-point
cat > "${APPDIR}/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "${HERE}/usr/lib/keepass/KeePass.Desktop.Avalonia" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

# Desktop file (required by AppImage spec to be at root)
cp "${SCRIPT_DIR}/org.keepass.desktop" "${APPDIR}/keepass.desktop"

# Icon at root (required by AppImage spec — use 256px if available)
ICON_256="${ICONS_DIR}/keepass-256.png"
if [[ -f "${ICON_256}" ]]; then
    cp "${ICON_256}" "${APPDIR}/keepass.png"
fi

# All icon sizes for proper desktop integration
for SIZE in 16 32 48 128 256 512; do
    ICON_SRC="${ICONS_DIR}/keepass-${SIZE}.png"
    if [[ -f "${ICON_SRC}" ]]; then
        ICON_DEST="${APPDIR}/usr/share/icons/hicolor/${SIZE}x${SIZE}/apps"
        mkdir -p "${ICON_DEST}"
        cp "${ICON_SRC}" "${ICON_DEST}/keepass.png"
    fi
done

OUTPUT_APPIMAGE="${OUTPUT_DIR}/KeePass-${VERSION}-x86_64.AppImage"

ARCH=x86_64 "${APPIMAGE_TOOL}" "${APPDIR}" "${OUTPUT_APPIMAGE}"
chmod +x "${OUTPUT_APPIMAGE}"

echo "==> AppImage ready: ${OUTPUT_APPIMAGE}"
