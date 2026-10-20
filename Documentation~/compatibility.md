# Compatibility

## Unity

The package manifest declares Unity `2021.3.0f1` as its minimum. That value is
an eligibility floor, not a record of testing against every later Editor
release.

The product code has one dependency-free Editor assembly:

```text
ProjectIcon.Editor
```

It contains no runtime code and exposes no public API.

The source selects APIs by Editor version:

- Unity 2021.3 through 2022.2 polls application focus during the existing Editor
  update callback. Unity 2022.3 and newer uses the public focus-change event.
- Unity 6.5 and newer explicitly preserves icon scheduling state when entering
  or leaving Play Mode without domain reload.

### Play Mode lifecycle

Domain reload can be disabled. Icon reapply timers belong to the Editor session
and persist across Play sessions. On Unity 6.5 and newer,
`OnCodeInitializing` and `OnCodeUnloading` register and release the Editor
callbacks. Older Editors use `InitializeOnLoadMethod` and
`AssemblyReloadEvents.beforeAssemblyReload`. Repeated initialization retains
one update callback, and unloading removes pending work without resetting the
native icon between code loads.

See Unity's [code lifecycle guidance](https://docs.unity.com/en-us/engine/7000.0/manual/programming-environment/code-reload-serialization/code-reload/code-lifecycle).

On 2026-10-10, all 18 package tests passed in Unity `7000.0.0a8` on macOS,
with no failures or skips. This includes three Play sessions without domain
reload and repeated callback setup/cleanup. The older Editor results below
predate these lifecycle changes.

### Verified Editors

On 2026-10-20, Unity `7000.0.0a10` passed all 17 package tests on macOS Apple
silicon, including texture-source PNG fallback and three Play sessions without
domain reload. The rebuilt universal bundle passed separate native cache,
invalidation, pinning, exception recovery, reset, ABI, architecture, and signing
checks. The running Editor retained its previously loaded native library;
the separate native process verified the new binary. Intel execution and older
Editor versions were not rerun for this pass.

On 2026-10-08, all 16 EditMode tests passed with no failures or skips on macOS
Apple silicon in `2021.3.10f1`, `2022.3.62f3`, `6000.0.68f1`, `6000.5.10f1`,
`6000.7.0b2`, and `7000.0.0a8`. These tests include applying and resetting both
native icon modes, loading the package icon texture, and checking plugin import
settings. They do not establish coverage for every patch release or host OS.

The native bundle importer retains serialization version 2 so Unity 2021.3 and
2022.3 can read its macOS Editor settings. Preserve that format and its GUID
when changing plugin settings, then validate in both an older Editor and Unity 7.

## Platforms

| Editor platform | Support | Notes |
| --- | --- | --- |
| macOS on Intel | Supported | Native slice targets macOS 10.13 or newer |
| macOS on Apple silicon | Supported | Native slice targets macOS 11 or newer |
| Windows | Settings only | No Windows icon backend is included |
| Linux | Settings only | No X11 or Wayland icon backend is included |

On unsupported platforms, the managed assembly and Preferences page still
load. Choices are saved for later use on a supported Mac, but no native icon
call is attempted.

The native bundle importer is restricted to the macOS Editor. It is not a
Player plugin and is not included as a Windows or Linux binary.

## Compile Symbols

This symbol disables the managed product and test assemblies:

- `PROJECT_ICON_DISABLE_ALL`

The native macOS bundle has no define constraint and remains installed. Turn
off Auto Apply before adding a symbol so the current process resets its icon.

## Package Identities

The package uses these integration identities:

| Contract | Value |
| --- | --- |
| Managed assembly | `ProjectIcon.Editor` |
| Managed namespace | `ProjectIcon.Editor` |
| Native bundle | `ProjectIcon.bundle` |
| Bundle identifier | `com.martincalander.projecticon.native` |
| Cache directory | `Library/com.martincalander.projecticon` |
| Preference prefix | `com.martincalander.projecticon` |

The package ID is `com.martincalander.projecticon`. Repository naming does not
change that package identity. Unity asset GUIDs remain unchanged. The current
preference keys start with their defaults when upgrading from an earlier
package identity.

See [Native bundle notes](native-bundle.md) for the exported ABI and rebuild
workflow.
