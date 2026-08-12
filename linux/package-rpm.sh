#!/usr/bin/env bash
# package-rpm.sh — Build a .rpm package for a given architecture.
#
# Usage:
#   ./linux/package-rpm.sh <rid> [<output_dir>]
#
# Dependencies: fpm (gem install fpm), rpmbuild
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

RID="${1:?Usage: $0 <rid>  (e.g. linux-x64)}"
OUTPUT_DIR="${2:-${REPO_ROOT}/artifacts/linux}"
VERSION="${KEEPASS_VERSION:-2.61.1}"

case "${RID}" in
    linux-x64)    RPM_ARCH="x86_64" ;;
    linux-arm64)  RPM_ARCH="aarch64" ;;
    *) echo "Unknown RID: ${RID}" >&2; exit 1 ;;
esac

PUBLISH_DIR="${OUTPUT_DIR}/publish-${RID}"
ICONS_DIR="${SCRIPT_DIR}/icons"

if [[ ! -d "${PUBLISH_DIR}" ]]; then
    echo "Error: ${PUBLISH_DIR} not found. Run build-linux.sh first." >&2
    exit 1
fi

echo "==> Creating .rpm package (${RPM_ARCH}, v${VERSION})…"

STAGE="$(mktemp -d)"
trap 'rm -rf "${STAGE}"' EXIT

mkdir -p "${STAGE}/opt/keepass"
cp -R "${PUBLISH_DIR}/." "${STAGE}/opt/keepass/"
chmod +x "${STAGE}/opt/keepass/KeePass.Desktop.Avalonia"

mkdir -p "${STAGE}/usr/bin"
cat > "${STAGE}/usr/bin/keepass" <<'WRAPPER'
#!/bin/sh
exec /opt/keepass/KeePass.Desktop.Avalonia "$@"
WRAPPER
chmod +x "${STAGE}/usr/bin/keepass"

mkdir -p "${STAGE}/usr/share/applications"
cp "${SCRIPT_DIR}/org.keepass.desktop" \
   "${STAGE}/usr/share/applications/"

mkdir -p "${STAGE}/usr/share/mime/packages"
cp "${SCRIPT_DIR}/org.keepass.desktop.xml" \
   "${STAGE}/usr/share/mime/packages/"

for SIZE in 16 32 48 128 256 512; do
    ICON_SRC="${ICONS_DIR}/keepass-${SIZE}.png"
    if [[ -f "${ICON_SRC}" ]]; then
        ICON_DEST="${STAGE}/usr/share/icons/hicolor/${SIZE}x${SIZE}/apps"
        mkdir -p "${ICON_DEST}"
        cp "${ICON_SRC}" "${ICON_DEST}/keepass.png"
    fi
done

POST_INSTALL="$(mktemp)"
cat > "${POST_INSTALL}" <<'HOOK'
#!/bin/sh
update-desktop-database -q /usr/share/applications || true
update-mime-database /usr/share/mime || true
gtk-update-icon-cache -q /usr/share/icons/hicolor || true
HOOK

fpm \
    --input-type dir \
    --output-type rpm \
    --name keepass \
    --version "${VERSION}" \
    --architecture "${RPM_ARCH}" \
    --description "KeePass 2 Password Manager (Avalonia cross-platform edition)" \
    --url "https://keepass.info" \
    --license "GPL-2.0-only" \
    --maintainer "KeePass Contributors" \
    --after-install "${POST_INSTALL}" \
    --after-remove "${POST_INSTALL}" \
    --package "${OUTPUT_DIR}/keepass-${VERSION}-${RPM_ARCH}.rpm" \
    --chdir "${STAGE}" \
    .

echo "==> .rpm ready: ${OUTPUT_DIR}/keepass-${VERSION}-${RPM_ARCH}.rpm"
