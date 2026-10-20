#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
TEST_DIR="$(mktemp -d "${TMPDIR:-/tmp}/project-icon-tests.XXXXXX")"
trap 'rm -rf "$TEST_DIR"' EXIT

clang++ -fobjc-arc -framework Cocoa "$SCRIPT_DIR/tests.mm" -o "$TEST_DIR/native-tests"
"$TEST_DIR/native-tests" \
  "$PACKAGE_DIR/Editor/Plugins/macOS/ProjectIcon.bundle/Contents/MacOS/ProjectIcon" \
  "$PACKAGE_DIR/ProjectIcon.png" "$@"
