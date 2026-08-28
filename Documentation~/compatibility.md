# Compatibility

## Unity

The package manifest declares Unity `2021.3.0f1` as its minimum. That value is
an eligibility floor, not a record of testing against every later Editor
release.

The product code has one dependency-free Editor assembly:

```text
Essentials.EditorIcon.Editor
```

It contains no runtime code and exposes no public API.

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

Any of these symbols disables the managed product and test assemblies:

- `ESSENTIALS_DISABLE_PROJECT_ICON`
- `ESSENTIALS_DISABLE_EDITOR_ICON`
- `ESSENTIALS_DISABLE_ALL`

`ESSENTIALS_DISABLE_EDITOR_ICON` is the established compatibility symbol from
the earlier package identity.

The native macOS bundle has no define constraint and remains installed. Turn
off Auto Apply before adding a symbol so the current process resets its icon.

## Preserved Identities

The standalone package keeps the integration identities that existing projects
already use:

| Contract | Value |
| --- | --- |
| Managed assembly | `Essentials.EditorIcon.Editor` |
| Managed namespace | `Essentials.EditorIcon.Editor` |
| Native bundle | `EssentialsEditorIcon.bundle` |
| Bundle identifier | `com.essentials.editoricon.native` |
| Cache directory | `Library/Essentials/EditorIcon` |
| Preference prefix | `Essentials.EditorIcon` |

The package ID is `com.martincalander.projecticon`. Repository naming does not
change that package identity.

See [Native bundle notes](native-bundle.md) for the exported ABI and rebuild
workflow.
