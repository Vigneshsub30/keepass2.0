#!/usr/bin/env bash
# build-macos.sh — Build self-contained KeePass .app bundles for macOS.
#
# Usage:
#   ./macOS/build-macos.sh [--version <version>] [--output <dir>]
#
# Environment:
#   KEEPASS_VERSION  (optional) — overrides the bundle version, e.g. "2.61.1"
#   OUTPUT_DIR       (optional) — overrides the default output directory
#
# Outputs (relative to OUTPUT_DIR):
#   KeePass-osx-arm64.app/
#   KeePass-osx-x64.app/
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# ── Configuration ──────────────────────────────────────────────────────── #
VERSION="${KEEPASS_VERSION:-2.61.1}"
OUTPUT_DIR="${OUTPUT_DIR:-${REPO_ROOT}/artifacts/macos}"
PROJECT="${REPO_ROOT}/KeePass.Desktop.Avalonia/KeePass.Desktop.Avalonia.csproj"
BUNDLE_ID="com.dominik-reichl.keepass"
EXECUTABLE="KeePass.Desktop.Avalonia"
ICON_ICNS="${SCRIPT_DIR}/KeePass.icns"

RID_ARM64="osx-arm64"
RID_X64="osx-x64"

echo "==> Building KeePass macOS bundles (version ${VERSION})"
mkdir -p "${OUTPUT_DIR}"

assemble_bundle() {
    local rid="$1"
    local app_dir="${OUTPUT_DIR}/KeePass-${rid}.app"
    local publish_dir="${OUTPUT_DIR}/publish-${rid}"
    local contents="${app_dir}/Contents"
    local macos_bin="${contents}/MacOS"
    local resources="${contents}/Resources"

    echo "  -> Publishing ${rid}…"
    dotnet publish "${PROJECT}" \
        --runtime "${rid}" \
        --configuration Release \
        --self-contained true \
        -p:PublishSingleFile=false \
        -p:UseAppHost=true \
        -p:Version="${VERSION}" \
        --output "${publish_dir}"

    echo "  -> Assembling .app bundle for ${rid}…"
    rm -rf "${app_dir}"
    mkdir -p "${macos_bin}" "${resources}"

    # Copy all published files into Contents/MacOS
    cp -R "${publish_dir}/." "${macos_bin}/"

    # Place Info.plist with version substitution
    sed "s/2\.61\.1\.0/${VERSION}.0/g; s/2\.61\.1/${VERSION}/g" \
        "${SCRIPT_DIR}/Info.plist" > "${contents}/Info.plist"

    # Copy icon if present
    if [[ -f "${ICON_ICNS}" ]]; then
        cp "${ICON_ICNS}" "${resources}/KeePass.icns"
    else
        echo "  [WARN] ${ICON_ICNS} not found — bundle will have no icon"
    fi

    echo "  -> Bundle ready: ${app_dir}"
}

assemble_bundle "${RID_ARM64}"
assemble_bundle "${RID_X64}"

echo "==> Build complete. Output: ${OUTPUT_DIR}"
