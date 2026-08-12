#!/usr/bin/env bash
# build/macos/build-dmg.sh
#
# Creates a signed and notarized .dmg from a dotnet publish layout.
#
# Usage:
#   build/macos/build-dmg.sh \
#       --version 2.61.1 \
#       --rid osx-arm64 \
#       --publish-dir ./artifacts/macos/publish/osx-arm64 \
#       --output-dir  ./artifacts/macos
#
# Prerequisites:
#   - Xcode Command Line Tools (codesign, hdiutil, xcrun)
#   - A Developer ID Application certificate imported into the keychain
#     (performed by a preceding CI step; see build.yml)
#   - APPLE_DEVELOPER_ID environment variable set to the signing identity
#     (e.g. "Developer ID Application: Acme Corp (TEAMID)")
#   - For notarization:
#       APPLE_API_KEY_PATH — path to the .p8 API key file
#       APPLE_API_KEY_ID   — 10-character key ID
#       APPLE_API_ISSUER   — UUID of the App Store Connect issuer
#
# The script exits non-zero on any failure.  Individual signing and
# notarization steps re-try up to 3 times with exponential back-off.

set -euo pipefail

# ── Argument parsing ──────────────────────────────────────────────────────────
VERSION=""
RID=""
PUBLISH_DIR=""
OUTPUT_DIR=""
SKIP_NOTARIZE="${SKIP_NOTARIZE:-false}"   # set to "true" in PR builds
SKIP_SIGNING="${SKIP_SIGNING:-false}"    # set to "true" for unsigned DMGs

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)    VERSION="$2";      shift 2 ;;
        --rid)        RID="$2";          shift 2 ;;
        --publish-dir) PUBLISH_DIR="$2"; shift 2 ;;
        --output-dir) OUTPUT_DIR="$2";   shift 2 ;;
        --skip-notarize) SKIP_NOTARIZE="true"; shift ;;
        --skip-signing)  SKIP_SIGNING="true"; SKIP_NOTARIZE="true"; shift ;;
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
ENTITLEMENTS="$SCRIPT_DIR/entitlements.plist"

APP_NAME="KeePass"
APP_BUNDLE="${OUTPUT_DIR}/${APP_NAME}-${RID}.app"
DMG_NAME="${APP_NAME}-${VERSION}-${RID}.dmg"
DMG_PATH="${OUTPUT_DIR}/${DMG_NAME}"

# ── Assemble .app bundle structure ────────────────────────────────────────────
echo "==> Assembling .app bundle: $APP_BUNDLE"

rm -rf "$APP_BUNDLE"
mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"

# Copy publish output into the MacOS directory.
cp -R "${PUBLISH_DIR}/." "${APP_BUNDLE}/Contents/MacOS/"

# Include keepass-proxy binary if it exists alongside the publish output
PROXY_DIR="$(dirname "$PUBLISH_DIR")/proxy"
if [[ -d "$PROXY_DIR" ]]; then
    echo "    Including keepass-proxy from: $PROXY_DIR"
    cp "${PROXY_DIR}/keepass-proxy" "${APP_BUNDLE}/Contents/MacOS/keepass-proxy" 2>/dev/null || \
    cp "${PROXY_DIR}/keepass-proxy.dll" "${APP_BUNDLE}/Contents/MacOS/keepass-proxy.dll" 2>/dev/null || true
    chmod +x "${APP_BUNDLE}/Contents/MacOS/keepass-proxy" 2>/dev/null || true
elif [[ -f "${PUBLISH_DIR}/keepass-proxy" ]]; then
    echo "    keepass-proxy found in publish dir"
    chmod +x "${APP_BUNDLE}/Contents/MacOS/keepass-proxy"
fi

# Info.plist — generated here so version is embedded at package time.
INFO_PLIST="${APP_BUNDLE}/Contents/Info.plist"
# Use the version as both CFBundleShortVersionString (marketing) and
# CFBundleVersion (build number).  This satisfies notarytool requirements.
cat > "$INFO_PLIST" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>       <string>org.keepass.keepass</string>
    <key>CFBundleName</key>             <string>KeePass</string>
    <key>CFBundleDisplayName</key>      <string>KeePass Password Safe</string>
    <key>CFBundleVersion</key>          <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundlePackageType</key>      <string>APPL</string>
    <key>CFBundleSignature</key>        <string>????</string>
    <key>CFBundleExecutable</key>       <string>KeePass.Desktop.Avalonia</string>
    <key>LSMinimumSystemVersion</key>   <string>12.0</string>
    <key>NSHighResolutionCapable</key>  <true/>
    <key>CFBundleIconFile</key>         <string>AppIcon</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2003-2026 Dominik Reichl. All rights reserved.</string>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>          <string>KeePass Database</string>
            <key>CFBundleTypeExtensions</key>    <array><string>kdbx</string></array>
            <key>CFBundleTypeRole</key>          <string>Editor</string>
            <key>LSIsAppleDefaultForType</key>   <true/>
        </dict>
    </array>
</dict>
</plist>
PLIST

# ── Retry helper ──────────────────────────────────────────────────────────────
retry() {
    local max="$1"; shift
    local delay=10
    local attempt=1
    while true; do
        "$@" && return 0
        if (( attempt >= max )); then
            echo "Command failed after $max attempts: $*" >&2
            return 1
        fi
        echo "Attempt $attempt/$max failed — retrying in ${delay}s …"
        sleep "$delay"
        delay=$(( delay * 2 ))
        attempt=$(( attempt + 1 ))
    done
}

# ── Code sign ─────────────────────────────────────────────────────────────────
DEVELOPER_ID="${APPLE_DEVELOPER_ID:-}"
if [[ "$SKIP_SIGNING" != "true" && -z "$DEVELOPER_ID" ]]; then
    # Auto-detect the first Developer ID Application identity in the keychain.
    DEVELOPER_ID=$(security find-identity -v -p codesigning \
        | grep "Developer ID Application" | head -1 \
        | sed 's/.*"\(.*\)"/\1/' || true)
fi

if [[ "$SKIP_SIGNING" != "true" && -z "$DEVELOPER_ID" ]]; then
    echo "WARNING: No Developer ID Application certificate found — producing unsigned DMG." >&2
    SKIP_SIGNING="true"
    SKIP_NOTARIZE="true"
fi

if [[ "$SKIP_SIGNING" != "true" ]]; then
    echo "==> Signing with identity: $DEVELOPER_ID"

    retry 3 codesign \
        --force \
        --deep \
        --sign "$DEVELOPER_ID" \
        --entitlements "$ENTITLEMENTS" \
        --options runtime \
        --timestamp \
        "$APP_BUNDLE"

    echo "==> Verifying code signature …"
    codesign --verify --deep --strict "$APP_BUNDLE"
    echo "    Signature OK."
else
    echo "==> Skipping code signing (unsigned build)."
fi

# ── Create DMG ────────────────────────────────────────────────────────────────
echo "==> Creating DMG: $DMG_PATH"

rm -f "$DMG_PATH"

# Two-step creation: hdiutil create -srcfolder can miscalculate the volume
# size for large single-file .NET self-contained binaries, causing
# "No space left on device".  Instead, create a sparse image with an
# explicit size, copy the .app, then convert to compressed UDZO.
APP_SIZE_MB=$(du -sm "$APP_BUNDLE" | awk '{print $1}')
SPARSE_SIZE=$(( APP_SIZE_MB + 20 ))m
SPARSE_IMG="$(mktemp -t keepass_dmg).sparseimage"
MOUNT_POINT="$(mktemp -d -t keepass_mount)"

hdiutil create -size "$SPARSE_SIZE" -volname "KeePass Password Safe" \
    -fs HFS+ -type SPARSE "${SPARSE_IMG%.sparseimage}"
hdiutil attach "$SPARSE_IMG" -mountpoint "$MOUNT_POINT"
cp -R "$APP_BUNDLE" "$MOUNT_POINT/"
hdiutil detach "$MOUNT_POINT"
hdiutil convert "$SPARSE_IMG" -format UDZO -o "$DMG_PATH" -ov
rm -f "$SPARSE_IMG"
rmdir "$MOUNT_POINT" 2>/dev/null || true

if [[ "$SKIP_SIGNING" != "true" ]]; then
    # Sign the DMG itself so Gatekeeper accepts it as a distributable.
    retry 3 codesign \
        --sign "$DEVELOPER_ID" \
        --timestamp \
        "$DMG_PATH"
fi

# ── Notarize ─────────────────────────────────────────────────────────────────
if [[ "$SKIP_NOTARIZE" == "true" ]]; then
    echo "==> Notarization skipped (SKIP_NOTARIZE=true)."
else
    API_KEY_PATH="${APPLE_API_KEY_PATH:-}"
    API_KEY_ID="${APPLE_API_KEY_ID:-}"
    API_ISSUER="${APPLE_API_ISSUER:-}"

    if [[ -z "$API_KEY_PATH" || -z "$API_KEY_ID" || -z "$API_ISSUER" ]]; then
        echo "ERROR: Notarization requires APPLE_API_KEY_PATH, APPLE_API_KEY_ID," >&2
        echo "       and APPLE_API_ISSUER environment variables." >&2
        exit 1
    fi

    echo "==> Submitting $DMG_PATH to notarytool …"
    retry 3 xcrun notarytool submit "$DMG_PATH" \
        --key       "$API_KEY_PATH" \
        --key-id    "$API_KEY_ID" \
        --issuer    "$API_ISSUER" \
        --wait \
        --timeout   30m

    echo "==> Stapling notarization ticket …"
    retry 3 xcrun stapler staple "$DMG_PATH"

    echo "==> Validating stapled ticket …"
    xcrun stapler validate "$DMG_PATH"
fi

# ── Emit checksum ─────────────────────────────────────────────────────────────
HASH=$(shasum -a 256 "$DMG_PATH" | awk '{print $1}')
echo "DMG SHA-256: $HASH  $DMG_PATH"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    echo "| DMG ($RID) | \`${DMG_NAME}\` | \`${HASH}\` |" >> "$GITHUB_STEP_SUMMARY"
fi

echo "==> DMG packaging complete: $DMG_PATH"
