#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULE_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SOURCE="$SCRIPT_DIR/EssentialsEditorIcon.mm"
BUNDLE_DIR="$MODULE_DIR/Plugins/macOS/EssentialsEditorIcon.bundle"
EXECUTABLE_DIR="$BUNDLE_DIR/Contents/MacOS"
EXECUTABLE="$EXECUTABLE_DIR/EssentialsEditorIcon"
BUILD_DIR="$(mktemp -d "${TMPDIR:-/tmp}/essentials-editoricon.XXXXXX")"

cleanup() {
  rm -rf "$BUILD_DIR"
}
trap cleanup EXIT

mkdir -p "$EXECUTABLE_DIR" "$BUILD_DIR"

clang++ -bundle -fobjc-arc -framework Cocoa -arch x86_64 -mmacosx-version-min=10.13 "$SOURCE" -o "$BUILD_DIR/EssentialsEditorIcon-x86_64"
clang++ -bundle -fobjc-arc -framework Cocoa -arch arm64 -mmacosx-version-min=11.0 "$SOURCE" -o "$BUILD_DIR/EssentialsEditorIcon-arm64"
lipo -create "$BUILD_DIR/EssentialsEditorIcon-x86_64" "$BUILD_DIR/EssentialsEditorIcon-arm64" -output "$EXECUTABLE"

if command -v codesign >/dev/null 2>&1; then
  codesign --force --sign - "$EXECUTABLE" >/dev/null
fi
