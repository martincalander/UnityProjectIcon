#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SOURCE="$SCRIPT_DIR/ProjectIcon.mm"
BUNDLE_DIR="$PACKAGE_DIR/Editor/Plugins/macOS/ProjectIcon.bundle"
EXECUTABLE_DIR="$BUNDLE_DIR/Contents/MacOS"
EXECUTABLE="$EXECUTABLE_DIR/ProjectIcon"
BUILD_DIR="$(mktemp -d "${TMPDIR:-/tmp}/project-icon.XXXXXX")"

cleanup() {
  rm -rf "$BUILD_DIR"
}
trap cleanup EXIT

mkdir -p "$EXECUTABLE_DIR" "$BUILD_DIR"

clang++ -bundle -O2 -Wall -Wextra -fobjc-arc -framework Cocoa -arch x86_64 -mmacosx-version-min=10.13 "$SOURCE" -o "$BUILD_DIR/ProjectIcon-x86_64"
clang++ -bundle -O2 -Wall -Wextra -fobjc-arc -framework Cocoa -arch arm64 -mmacosx-version-min=11.0 "$SOURCE" -o "$BUILD_DIR/ProjectIcon-arm64"
lipo -create "$BUILD_DIR/ProjectIcon-x86_64" "$BUILD_DIR/ProjectIcon-arm64" -output "$EXECUTABLE"
lipo "$EXECUTABLE" -verify_arch x86_64
lipo "$EXECUTABLE" -verify_arch arm64

nm -gU "$EXECUTABLE" > "$BUILD_DIR/exported-symbols.txt"
grep -q '_ProjectIconSetApplicationIcon$' "$BUILD_DIR/exported-symbols.txt"
grep -q '_ProjectIconSetApplicationIconWithMode$' "$BUILD_DIR/exported-symbols.txt"
grep -q '_ProjectIconResetApplicationIcon$' "$BUILD_DIR/exported-symbols.txt"

if command -v codesign >/dev/null 2>&1; then
  codesign --force --sign - "$EXECUTABLE" >/dev/null
  codesign --force --sign - "$BUNDLE_DIR" >/dev/null
  codesign --verify --deep --strict "$BUNDLE_DIR"
fi
