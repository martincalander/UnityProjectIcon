# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Changed

- Renamed the package and repository from `com.essentials.editoricon` to `com.essentials.projecticon`, with the user-facing name **Project Icon**.
- Added `ESSENTIALS_DISABLE_PROJECT_ICON` while retaining the existing assembly, namespace, preference, native ABI, Unity GUID, and `ESSENTIALS_DISABLE_EDITOR_ICON` compatibility contracts.

## [0.2.0] - 2026-08-22

### Added

- Restored Editor Icon as a standalone package after its temporary integration into `com.essentials.extensions`.
- Added user-scoped auto-apply preferences and focused EditMode coverage.
- Added package-owned native bundle, importer, stable-identity, and removed legacy-menu contract tests.

### Changed

- Normalized the package to root `Editor`, `Native~`, and `Tests/Editor` folders while preserving existing Unity asset GUIDs, managed assembly names, namespace, settings key, cache path, native bundle identifier, and exported entry points.
- Restricted the native bundle importer explicitly to the macOS Editor.
- Updated the native build script for the standalone package layout and whole-bundle signing and validation.

## [0.1.0] - 2026-07-12

### Added

- Added automatic macOS Unity Editor icon badging from the active Player icon.
- Added the universal Intel and Apple-silicon AppKit bundle, managed native bridge, and native build source.
