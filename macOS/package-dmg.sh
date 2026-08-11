#!/usr/bin/env bash
# package-dmg.sh — Create a .dmg disk image from a signed .app bundle.
#
# Usage:
#   ./macOS/package-dmg.sh <app_bundle_path> [<output_dmg_path>]
#
# If <output_dmg_path> is omitted the .dmg is placed alongside the .app.
#
set -euo pipefail

APP_BUNDLE="${1:?Usage: $0 <app_bundle_path> [<output_dmg_path>]}"
APP_NAME="$(basename "${APP_BUNDLE}" .app)"
OUTPUT_DMG="${2:-$(dirname "${APP_BUNDLE}")/${APP_NAME}.dmg}"

if [[ ! -d "${APP_BUNDLE}" ]]; then
    echo "Error: ${APP_BUNDLE} is not a directory" >&2
    exit 1
fi

STAGING_DIR="$(mktemp -d)"
trap 'rm -rf "${STAGING_DIR}"' EXIT

echo "==> Creating staging layout…"
cp -R "${APP_BUNDLE}" "${STAGING_DIR}/"
# Symlink to /Applications so users can drag-and-drop
ln -s /Applications "${STAGING_DIR}/Applications"

echo "==> Creating .dmg: ${OUTPUT_DMG}"
hdiutil create \
    -volname "KeePass" \
    -srcfolder "${STAGING_DIR}" \
    -ov \
    -format UDZO \
    "${OUTPUT_DMG}"

echo "==> DMG ready: ${OUTPUT_DMG}"
