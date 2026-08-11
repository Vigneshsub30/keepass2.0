#!/usr/bin/env bash
# build/linux/build-deb.sh
#
# Packages a dotnet publish layout as a Debian (.deb) package using fpm.
#
# Usage:
#   build/linux/build-deb.sh \
#       --version   2.61.1 \
#       --rid       linux-x64 \
#       --publish-dir ./artifacts/linux/publish/linux-x64 \
#       --output-dir  ./artifacts/linux
#
# Prerequisites on the runner (installed by the workflow):
#   - ruby + fpm gem:  sudo gem install fpm --no-document
#   - dpkg-deb (part of dpkg package, pre-installed on ubuntu-latest)
#
# Output: $OUTPUT_DIR/keepass2-$VERSION_$DEB_ARCH.deb
#
# The .deb installs:
#   /usr/lib/keepass2/          — KeePass binaries
#   /usr/bin/keepass2           — wrapper launcher script
#   /usr/share/applications/   — .desktop entry
#   /usr/share/icons/hicolor/  — PNG icons (48x48, 64x64, 128x128, 256x256)
#   /usr/share/mime/packages/  — MIME type XML for .kdbx association

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

# ── Map RID to Debian architecture ────────────────────────────────────────────
case "$RID" in
    linux-x64)   DEB_ARCH="amd64" ;;
    linux-arm64) DEB_ARCH="arm64" ;;
    linux-arm)   DEB_ARCH="armhf" ;;
    *) echo "Unsupported RID for .deb: $RID" >&2; exit 1 ;;
esac

# ── Stage the filesystem layout ───────────────────────────────────────────────
STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT

LIB_DIR="${STAGING}/usr/lib/keepass2"
BIN_DIR="${STAGING}/usr/bin"
APP_DIR="${STAGING}/usr/share/applications"
MIME_DIR="${STAGING}/usr/share/mime/packages"
ICONS_BASE="${STAGING}/usr/share/icons/hicolor"

mkdir -p "$LIB_DIR" "$BIN_DIR" "$APP_DIR" "$MIME_DIR"

# Copy publish output.
cp -R "${PUBLISH_DIR}/." "$LIB_DIR/"
chmod +x "${LIB_DIR}/KeePass" 2>/dev/null || true

# Wrapper launcher — invoked as 'keepass2' from $PATH.
cat > "${BIN_DIR}/keepass2" <<'WRAPPER'
#!/bin/sh
exec /usr/lib/keepass2/KeePass "$@"
WRAPPER
chmod 755 "${BIN_DIR}/keepass2"

# .desktop entry.
DESKTOP_SRC="${REPO_ROOT}/linux/org.keepass.desktop"
if [[ -f "$DESKTOP_SRC" ]]; then
    cp "$DESKTOP_SRC" "${APP_DIR}/org.keepass.desktop"
else
    # Minimal fallback .desktop when the repo file is absent.
    cat > "${APP_DIR}/org.keepass.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=KeePass Password Safe 2
GenericName=Password Manager
Comment=Manage your passwords securely
Exec=/usr/bin/keepass2 %F
Icon=keepass2
Categories=Utility;Security;
MimeType=application/x-keepass2;
Keywords=password;security;vault;kdbx;
StartupNotify=true
DESKTOP
fi

# MIME type definition.
MIME_SRC="${REPO_ROOT}/linux/org.keepass.desktop.xml"
if [[ -f "$MIME_SRC" ]]; then
    cp "$MIME_SRC" "${MIME_DIR}/keepass2.xml"
else
    cat > "${MIME_DIR}/keepass2.xml" <<MIMEXML
<?xml version="1.0" encoding="utf-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
  <mime-type type="application/x-keepass2">
    <comment>KeePass 2 Database</comment>
    <glob pattern="*.kdbx"/>
  </mime-type>
</mime-info>
MIMEXML
fi

# Icons — copy any .png files found alongside the published binaries, or
# generate placeholder icon directories so fpm succeeds.
for SIZE in 48 64 128 256; do
    ICON_DIR="${ICONS_BASE}/${SIZE}x${SIZE}/apps"
    mkdir -p "$ICON_DIR"
    # Look for icon files named keepass2.png or keepass*.png in the repo.
    ICON_SRC=$(find "$REPO_ROOT" -maxdepth 3 -name "keepass2.png" 2>/dev/null | head -1 || true)
    if [[ -n "$ICON_SRC" ]]; then
        cp "$ICON_SRC" "${ICON_DIR}/keepass2.png"
    fi
done

# ── Post-install / post-remove scripts ────────────────────────────────────────
POST_INSTALL="$(mktemp)"
cat > "$POST_INSTALL" <<'POSTINST'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications 2>/dev/null || true
update-mime-database /usr/share/mime 2>/dev/null || true
gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
POSTINST
chmod 755 "$POST_INSTALL"

POST_REMOVE="$(mktemp)"
cat > "$POST_REMOVE" <<'POSTRM'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications 2>/dev/null || true
update-mime-database /usr/share/mime 2>/dev/null || true
gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
POSTRM
chmod 755 "$POST_REMOVE"

# ── Build the .deb with fpm ───────────────────────────────────────────────────
DEB_FILE="${OUTPUT_DIR}/keepass2-${VERSION}_${DEB_ARCH}.deb"

echo "==> Building .deb: $DEB_FILE"

fpm \
    --input-type dir \
    --output-type deb \
    --name keepass2 \
    --version "$VERSION" \
    --architecture "$DEB_ARCH" \
    --description "KeePass Password Safe 2 — secure cross-platform password manager" \
    --url "https://keepass.info" \
    --maintainer "KeePass Development Team" \
    --license "GPL-2.0-only" \
    --vendor "Dominik Reichl" \
    --depends "libicu72 | libicu74 | libicu" \
    --category utils \
    --after-install "$POST_INSTALL" \
    --after-remove  "$POST_REMOVE" \
    --package "$DEB_FILE" \
    --force \
    "$STAGING/"=/ 

rm -f "$POST_INSTALL" "$POST_REMOVE"

# ── Verify ────────────────────────────────────────────────────────────────────
echo "==> Verifying .deb …"
dpkg-deb --info "$DEB_FILE" | grep -E "Package|Version|Architecture"
dpkg-deb --contents "$DEB_FILE" | grep -E "keepass|KeePass" | head -20

# ── Checksum ─────────────────────────────────────────────────────────────────
HASH=$(sha256sum "$DEB_FILE" | awk '{print $1}')
echo ".deb SHA-256: $HASH  $DEB_FILE"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    echo "| .deb ($RID / $DEB_ARCH) | \`$(basename "$DEB_FILE")\` | \`${HASH}\` |" >> "$GITHUB_STEP_SUMMARY"
fi

echo "==> .deb packaging complete: $DEB_FILE"
