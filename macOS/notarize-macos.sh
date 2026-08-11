#!/usr/bin/env bash
# notarize-macos.sh — Submit a .dmg to Apple notarization and staple the ticket.
#
# Usage:
#   ./macOS/notarize-macos.sh <dmg_path>
#
# Required environment variables:
#   APPLE_ID              — Apple ID email used for notarization
#   APPLE_TEAM_ID         — 10-character Apple Developer Team ID
#   APP_SPECIFIC_PASSWORD — App-specific password generated at appleid.apple.com
#
set -euo pipefail

DMG_PATH="${1:?Usage: $0 <dmg_path>}"
APPLE_ID="${APPLE_ID:?APPLE_ID env var is required}"
TEAM_ID="${APPLE_TEAM_ID:?APPLE_TEAM_ID env var is required}"
APP_PASSWORD="${APP_SPECIFIC_PASSWORD:?APP_SPECIFIC_PASSWORD env var is required}"

if [[ ! -f "${DMG_PATH}" ]]; then
    echo "Error: ${DMG_PATH} not found" >&2
    exit 1
fi

echo "==> Submitting ${DMG_PATH} to Apple notarization…"

SUBMISSION_ID=$(
    xcrun notarytool submit "${DMG_PATH}" \
        --apple-id  "${APPLE_ID}" \
        --team-id   "${TEAM_ID}" \
        --password  "${APP_PASSWORD}" \
        --output-format json \
        | python3 -c "import sys, json; print(json.load(sys.stdin)['id'])"
)

echo "    Submission ID: ${SUBMISSION_ID}"
echo "==> Waiting for notarization result (this may take several minutes)…"

xcrun notarytool wait "${SUBMISSION_ID}" \
    --apple-id  "${APPLE_ID}" \
    --team-id   "${TEAM_ID}" \
    --password  "${APP_PASSWORD}"

# Retrieve the full log for diagnostic purposes even on success
xcrun notarytool log "${SUBMISSION_ID}" \
    --apple-id  "${APPLE_ID}" \
    --team-id   "${TEAM_ID}" \
    --password  "${APP_PASSWORD}" \
    notarization.log || true

echo "==> Stapling notarization ticket to ${DMG_PATH}…"
xcrun stapler staple "${DMG_PATH}"

echo "==> Verifying…"
xcrun stapler validate "${DMG_PATH}"
spctl --assess --type open --context context:primary-signature "${DMG_PATH}" \
    && echo "==> Notarization check: PASS"

echo "==> Notarization complete."
