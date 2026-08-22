# Project Icon

`com.essentials.projecticon` gives each macOS Unity Editor project a recognizable Dock and application-switcher icon by compositing the active Player icon over Unity's application icon.

The package changes only the running Editor process. It does not modify the Unity installation, project icon assets, or Player builds.

## Requirements

- Unity 2021.3 or newer.
- macOS 10.13 or newer on Intel, or macOS 11 or newer on Apple silicon.

The managed Editor assembly also compiles on Windows and Linux, where icon application reports that the feature is macOS-only. The native bundle is explicitly imported for the macOS Editor only.

## Installation

Install the private repository as an embedded package:

```bash
git submodule add -b main \
  https://github.com/EssentialsForUnity/com.essentials.projecticon.git \
  Packages/com.essentials.projecticon
```

Or add the authenticated Git dependency to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.essentials.projecticon": "https://github.com/EssentialsForUnity/com.essentials.projecticon.git"
  }
}
```

## Usage

Assign an application icon in Unity's Player settings, then enable **Preferences > Essentials > Editor Icon > Auto Apply Player Icon Badge**. Auto-apply is enabled by default and persists as a personal `EditorPrefs` setting. The preference path retains its established name for compatibility.

The package reapplies the badge after project changes and Editor focus changes because Unity can restore its default application icon during normal Editor activity. Disabling auto-apply resets the icon.

To disable the assembly without removing an umbrella package that depends on it, define `ESSENTIALS_DISABLE_PROJECT_ICON`. The existing `ESSENTIALS_DISABLE_EDITOR_ICON` and `ESSENTIALS_DISABLE_ALL` symbols remain supported for compatibility.

## Native bundle

The distributable universal bundle is committed at `Editor/Plugins/macOS/EssentialsEditorIcon.bundle`, so installing the package does not require Xcode or command-line developer tools. Its stable native contract is:

- Bundle identifier: `com.essentials.editoricon.native`
- Architectures: `x86_64` and `arm64`
- Entry points: `EssentialsEditorIconSetApplicationIcon` and `EssentialsEditorIconResetApplicationIcon`

Native source is intentionally hidden from Unity under `Native~/macOS`. Rebuild and validate the checked-in bundle on macOS with:

```bash
./Native~/macOS/build.sh
```

Commit the rebuilt executable and signature resources together. Preserve the bundle name, identifier, exported entry points, architecture set, and Unity asset GUID because the managed bridge and existing projects depend on those identities.

The native `EssentialsEditorIcon` names, managed `Essentials.EditorIcon.*` assembly and namespace identities, preference key, and cache path intentionally retain their established values. Renaming the package does not break those existing contracts.

## Package layout

- `Editor/` contains the managed Editor assembly and macOS native bundle.
- `Native~/macOS/` contains the Objective-C++ source and reproducible build script.
- `Tests/Editor/` contains preference, legacy-menu, package identity, and native importer contract tests.

## License

This package is proprietary. See `LICENSE.md` for the applicable terms.
