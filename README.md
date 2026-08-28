# Project Icon

`com.martincalander.projecticon` gives each macOS Unity Editor project a recognizable Dock and application-switcher icon. The active Player icon can appear as a badge over Unity's icon or replace it completely.

The package changes only the running Editor process. It does not modify the Unity installation, project icon assets, or Player builds.

## Requirements

- Unity 2021.3 or newer.
- macOS 10.13 or newer on Intel, or macOS 11 or newer on Apple silicon.

## Platform support

| Unity Editor platform | Status | Behavior |
| --- | --- | --- |
| macOS | Supported | Badge and Icon Replacement modes update the current Editor process. |
| Windows | Not supported | The package loads and saves preferences, but does not change Editor icons. |
| Linux (X11, XWayland, or Wayland) | Not supported | The package loads and saves preferences, but does not change Editor icons. |

Windows needs a separate, tested implementation for its window icons and taskbar overlays. Project Icon does not ship that backend, so it does not call `WM_SETICON` or the Windows taskbar API and makes no icon changes on Windows.

Linux cannot provide the same reliable cross-desktop behavior through one integration. X11's [`_NET_WM_ICON`](https://specifications.freedesktop.org/wm/latest-single/#idm45159979263616) is only a per-window hint that a window manager or desktop shell may ignore. Native Wayland icon updates require Unity's own toplevel protocol object, which Unity does not expose to Editor packages. Project Icon therefore makes no icon changes on Linux rather than claiming partial support that may not appear in the GNOME or KDE dock/taskbar.

The managed Editor assembly compiles on Windows and Linux so the package can remain installed in cross-platform projects. Compilation does not imply icon-application support. The native bundle is imported for the macOS Editor only, and the Preferences page shows an explicit warning on unsupported platforms.

## Installation

Install the private repository as an embedded package:

```bash
git submodule add -b main \
  https://github.com/martincalander/com.martincalander.projecticon.git \
  Packages/com.martincalander.projecticon
```

Or add the authenticated Git dependency to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.martincalander.projecticon": "https://github.com/martincalander/com.martincalander.projecticon.git"
  }
}
```

## Usage

Assign an application icon in Unity's Player settings, then open **Preferences > Essentials > Project Icon**. Enable **Auto Apply Player Icon** and choose an **Appearance**:

- **Badge** composites the Player icon over Unity's icon. This remains the default for existing users.
- **Icon Replacement** uses the Player icon as the complete Editor application icon.

Both choices persist as personal `EditorPrefs` settings. The existing preference keys retain their established names, so changing the Preferences entry to **Project Icon** does not reset user choices.

On macOS, the package applies appearance changes immediately and reapplies the selected icon after project and Editor focus changes because Unity can restore its default application icon during normal Editor activity. Disabling auto-apply resets the icon.

To disable the assembly without removing an umbrella package that depends on it, define `ESSENTIALS_DISABLE_PROJECT_ICON`. The existing `ESSENTIALS_DISABLE_EDITOR_ICON` and `ESSENTIALS_DISABLE_ALL` symbols remain supported for compatibility.

## Native bundle

The distributable universal bundle is committed at `Editor/Plugins/macOS/EssentialsEditorIcon.bundle`, so installing the package does not require Xcode or command-line developer tools. Its stable native contract is:

- Bundle identifier: `com.essentials.editoricon.native`
- Architectures: `x86_64` and `arm64`
- Entry points: `EssentialsEditorIconSetApplicationIcon`, `EssentialsEditorIconSetApplicationIconWithMode`, and `EssentialsEditorIconResetApplicationIcon`

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
