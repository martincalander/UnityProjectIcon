# Native Bundle Notes

Normal package use does not require native developer tools. A universal bundle
is already checked in at:

```text
Editor/Plugins/macOS/EssentialsEditorIcon.bundle
```

This page is for maintainers changing the Objective-C++ bridge.

## Source and Build Script

The hidden UPM development folder contains:

```text
Native~/macOS/
├── EssentialsEditorIcon.mm
├── build.sh
├── test.sh
└── tests.mm
```

Run the build and validation script on macOS:

```bash
./Native~/macOS/build.sh
```

The script uses the developer tools available on the machine; compiler and SDK
versions are not pinned. It builds `x86_64` and `arm64` slices, combines them
with `lipo`, checks the exported symbols, and applies an ad-hoc signature when
`codesign` is available.

Run `bash Native~/macOS/test.sh` after rebuilding. It loads the built bundle in
a separate AppKit process and checks image reuse, source and appearance changes,
missing files, pinning, reset, native exception recovery, and both set-icon entry points. Add
`--benchmark-only` to time 40 repeated applies of an unchanged replacement icon.

## Stable Native Contract

| Contract | Value |
| --- | --- |
| Bundle name | `EssentialsEditorIcon.bundle` |
| Executable | `EssentialsEditorIcon` |
| Bundle identifier | `com.essentials.editoricon.native` |
| Architectures | `x86_64` and `arm64` |
| Intel deployment target | macOS 10.13 |
| Apple silicon deployment target | macOS 11 |

The managed bridge depends on these exported entry points:

```text
EssentialsEditorIconSetApplicationIcon
EssentialsEditorIconSetApplicationIconWithMode
EssentialsEditorIconResetApplicationIcon
```

The legacy set-icon entry point remains part of the compatibility contract.

## AppKit Integration

The bundle replaces AppKit's application-icon setter with a guarded
`class_replaceMethod` hook. While an icon is pinned, notification observers
reapply it when the application or a window changes state. Reset clears the
pinned image and removes those observers.

Repeated applies reuse the composed image while its source path, appearance,
and file metadata are unchanged. Replacing or modifying the source invalidates
that cache. Immediate and deferred reapply still run on cache hits.

AppKit exceptions are contained at native entry points and deferred callbacks.
A failed icon update restores the previous pinned image and cache state.
Image composition releases its graphics context even when drawing fails.

The method hook remains installed after reset, but behaves as a pass-through
when no image is pinned. Keep that lifecycle intact when changing the native
bridge; installing competing hooks or retaining observers after reset can make
Unity's icon state unpredictable.

## Before Committing a Rebuild

Confirm:

- both architecture slices are present;
- all three entry points are exported;
- the complete bundle passes `codesign --verify --deep --strict`;
- `Info.plist` keeps the bundle identifier and executable name;
- the bundle importer remains macOS Editor-only;
- the existing bundle `.meta` GUID is unchanged;
- reset clears the pinned image and notification observers while leaving the
  pass-through hook safe.

Commit the executable and any signature resources together. Run the focused
native contract EditMode tests on a Mac before treating the rebuilt bundle as
validated.
