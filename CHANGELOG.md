# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Fixed

- Restored compilation on Unity 6.0 and 6.1 by using integer instance IDs until
  the public entity ID API is available in Unity 6.2.
- Restored macOS compilation on Unity 2021.3 and early 2022 releases by polling
  application focus, using the public focus event from Unity 2022.3 onward.
- Restored native plugin loading on Unity 2021.3 and 2022.3 by saving the
  importer in the older plugin format without changing its GUID.

### Changed

- Paired icon callback registration with code-unload cleanup and added tests
  for repeated Play sessions with domain reload disabled.

- Explicitly preserve Editor icon scheduling and cached state across Play Mode
  transitions under Unity's newer static lifecycle system.
- Moved the repository to Martin Calander's personal GitHub account and renamed
  the package from `com.essentials.projecticon` to
  `com.martincalander.projecticon`, while preserving its managed, native,
  preference, and Unity asset compatibility identities.
- Renamed the GitHub repository from `com.martincalander.projecticon` to
  `UnityProjectIcon` without changing the package ID or installation path.
- Renamed the user Preferences entry from **Editor Icon** to **Project Icon** while preserving the existing `EditorPrefs` keys.
- Added focused user, compatibility, troubleshooting, and native-maintainer
  guides, including the default Auto Apply behavior, icon fallback order, and
  macOS-only support boundary.

## [0.3.0] - 2026-08-22

### Added

- Added a user-scoped Appearance preference for choosing a Unity-icon badge or a full Player-icon replacement.
- Added the mode-aware `EssentialsEditorIconSetApplicationIconWithMode` native entry point while retaining the existing badge entry point.

### Changed

- Renamed the package and repository from `com.essentials.editoricon` to `com.essentials.projecticon`, with the user-facing name **Project Icon**.
- Added `ESSENTIALS_DISABLE_PROJECT_ICON` while retaining the existing assembly, namespace, preference, native ABI, Unity GUID, and `ESSENTIALS_DISABLE_EDITOR_ICON` compatibility contracts.
- Clarified in the Preferences page and README that icon application is supported on macOS only; Windows and Linux load the package but do not change Editor icons.

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
