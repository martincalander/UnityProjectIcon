# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Fixed

- Retry with a readable PNG when AppKit cannot decode the Player texture's
  source file, including serialized Texture2D assets.
- Contain native AppKit exceptions during icon application, reset and deferred
  callbacks, and restore the previous pinned icon after a failed update.
- Fixed native bundle architecture validation with the current macOS `lipo` tool.
- Restored compilation on Unity 6.0 and 6.1 by using integer instance IDs until
  the public entity ID API is available in Unity 6.2.
- Restored macOS compilation on Unity 2021.3 and early 2022 releases by polling
  application focus, using the public focus event from Unity 2022.3 onward.
- Restored native plugin loading on Unity 2021.3 and 2022.3 by saving the
  importer in the older plugin format without changing its GUID.

### Changed

- Standardized managed assemblies and namespaces, native symbols and bundle,
  preference keys, cache paths, package artwork filename, and Preferences
  navigation on Project Icon. Existing Unity asset GUIDs are preserved.
- Replaced previous compile-disable symbols with `PROJECT_ICON_DISABLE_ALL`.
  Preferences use new keys and start with their default values after upgrading.
- Reuse the composed macOS icon for unchanged files during startup, focus and
  maintenance reapply, avoiding repeated image decoding and rendering.
- Removed unreachable managed icon-cache and scheduling state, and consolidated
  redundant package tests.

- Paired icon callback registration with code-unload cleanup and added tests
  for repeated Play sessions with domain reload disabled.

- Explicitly preserve Editor icon scheduling and cached state across Play Mode
  transitions under Unity's newer static lifecycle system.
- Moved the repository to Martin Calander's personal GitHub account and renamed
  the package to `com.martincalander.projecticon`.
- Renamed the GitHub repository from `com.martincalander.projecticon` to
  `UnityProjectIcon` without changing the package ID or installation path.
- Renamed the user Preferences entry to **Project Icon**.
- Added focused user, compatibility, troubleshooting, and native-maintainer
  guides, including the default Auto Apply behavior, icon fallback order, and
  macOS-only support boundary.

## [0.3.0] - 2026-08-22

### Added

- Added a user-scoped Appearance preference for choosing a Unity-icon badge or a full Player-icon replacement.
- Added a mode-aware native entry point while retaining the badge entry point.

### Changed

- Introduced the user-facing name **Project Icon**.
- Added a package-specific compile-disable option.
- Clarified in the Preferences page and README that icon application is supported on macOS only; Windows and Linux load the package but do not change Editor icons.

## [0.2.0] - 2026-08-22

### Added

- Restored the icon feature as a standalone package after its temporary integration into an extensions package.
- Added user-scoped auto-apply preferences and focused EditMode coverage.
- Added package-owned native bundle, importer, identity, and menu contract tests.

### Changed

- Normalized the package to root `Editor`, `Native~`, and `Tests/Editor` folders while preserving existing Unity asset GUIDs.
- Restricted the native bundle importer explicitly to the macOS Editor.
- Updated the native build script for the standalone package layout and whole-bundle signing and validation.

## [0.1.0] - 2026-07-12

### Added

- Added automatic macOS Unity Editor icon badging from the active Player icon.
- Added the universal Intel and Apple-silicon AppKit bundle, managed native bridge, and native build source.
