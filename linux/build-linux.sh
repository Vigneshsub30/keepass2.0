#!/usr/bin/env bash
# build-linux.sh — Publish self-contained KeePass binaries for Linux.
#
# Usage:
#   ./linux/build-linux.sh [--version <version>]
#
# Environment:
#   KEEPASS_VERSION  (optional) override version, e.g. "2.61.1"
#   OUTPUT_DIR       (optional) override output directory
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

VERSION="${KEEPASS_VERSION:-2.61.1}"
OUTPUT_DIR="${OUTPUT_DIR:-${REPO_ROOT}/artifacts/linux}"
PROJECT="${REPO_ROOT}/KeePass.Desktop.Avalonia/KeePass.Desktop.Avalonia.csproj"

echo "==> Building KeePass Linux binaries (version ${VERSION})"
mkdir -p "${OUTPUT_DIR}"

publish_rid() {
    local rid="$1"
    echo "  -> Publishing ${rid}…"
    dotnet publish "${PROJECT}" \
        --runtime "${rid}" \
        --configuration Release \
        --self-contained true \
        -p:PublishSingleFile=false \
        -p:UseAppHost=true \
        -p:Version="${VERSION}" \
        --output "${OUTPUT_DIR}/publish-${rid}"
    echo "  -> Published to ${OUTPUT_DIR}/publish-${rid}"
}

publish_rid "linux-x64"
publish_rid "linux-arm64"

echo "==> Build complete."
