#!/usr/bin/env bash
# sign-macos.sh — Sign a KeePass .app bundle with a Developer ID certificate.
#
# Usage:
#   ./macOS/sign-macos.sh <app_bundle_path>
#
# Required environment variables:
#   APPLE_DEVELOPER_ID   — The full certificate identity string, e.g.
#                          "Developer ID Application: Acme Corp (TEAMID)"
#
# Optional environment variables:
#   ENTITLEMENTS_PLIST   — Path to entitlements.plist
#                          (default: macOS/entitlements.plist next to this script)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

APP_BUNDLE="${1:?Usage: $0 <app_bundle_path>}"
DEVELOPER_ID="${APPLE_DEVELOPER_ID:?APPLE_DEVELOPER_ID env var is required}"
ENTITLEMENTS="${ENTITLEMENTS_PLIST:-${SCRIPT_DIR}/entitlements.plist}"

if [[ ! -d "${APP_BUNDLE}" ]]; then
    echo "Error: ${APP_BUNDLE} is not a directory" >&2
    exit 1
fi

echo "==> Signing ${APP_BUNDLE}"
echo "    Identity : ${DEVELOPER_ID}"
echo "    Entitlements: ${ENTITLEMENTS}"

# Sign all nested shared libraries and frameworks first, then the app itself.
# codesign --deep handles the top-level but explicit inner signing is more reliable.
find "${APP_BUNDLE}" \
    \( -name "*.dylib" -o -name "*.so" -o -name "*.framework" \) \
    -exec codesign \
        --force \
        --sign "${DEVELOPER_ID}" \
        --options runtime \
        --entitlements "${ENTITLEMENTS}" \
        --timestamp \
        {} \;

# Sign the main .app bundle
codesign \
    --force \
    --sign "${DEVELOPER_ID}" \
    --options runtime \
    --entitlements "${ENTITLEMENTS}" \
    --timestamp \
    "${APP_BUNDLE}"

echo "==> Verifying signature…"
codesign --verify --deep --strict "${APP_BUNDLE}"
spctl --assess --type execute "${APP_BUNDLE}" && echo "==> Gatekeeper check: PASS"

echo "==> Signing complete."
