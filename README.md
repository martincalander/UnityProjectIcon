# Project Icon

Tell your open Unity projects apart at a glance.

<p align="center">
  <img alt="UPM minimum 2021.3.0f1" src="https://img.shields.io/badge/UPM%20minimum-2021.3.0f1-222C37?logo=unity&logoColor=white">
  <img alt="macOS icon support" src="https://img.shields.io/badge/icon%20support-macOS-111?logo=apple&logoColor=white">
  <img alt="Editor-only package" src="https://img.shields.io/badge/scope-Editor%20only-555">
</p>

Project Icon borrows a Player icon from the current project and applies it to
the running Unity Editor on macOS. Use a small badge over Unity's icon or
replace the icon completely; either way, the right project is easier to spot in
the Dock and application switcher.

It changes only the current Editor process. The Unity installation, project
assets, and Player builds are left alone.

## Highlights

- Starts automatically on macOS; Auto Apply defaults on when no earlier
  preference exists.
- Offers **Badge** and **Icon Replacement** appearances; Badge is the first-run
  default.
- Reapplies after startup, focus changes, and project changes because Unity can
  restore its own icon during normal Editor use.
- Uses a checked-in universal native bundle for Intel and Apple silicon Macs.
- Loads and saves settings on Windows and Linux without making a native icon
  call.

## Requirements and Platform Support

| Environment | Status | Notes |
| --- | --- | --- |
| Unity `2021.3.0f1` | UPM minimum | Eligibility floor, not a full validation matrix |
| macOS on Intel | Icon support | macOS 10.13 or newer |
| macOS on Apple silicon | Icon support | macOS 11 or newer |
| Windows | Settings only | No native icon call |
| Linux | Settings only | No native icon call |
| Player/runtime | Not used | Every package assembly is Editor-only |

See [Compatibility](Documentation~/compatibility.md) for the native and
cross-platform boundaries.

## Installation

Both installation methods require Git and credentials that can read this
private repository.

Add it as an editable submodule:

```bash
git submodule add -b main \
  https://github.com/martincalander/UnityProjectIcon.git \
  Packages/com.martincalander.projecticon
```

Or add it as a read-only Git dependency in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.martincalander.projecticon": "https://github.com/martincalander/UnityProjectIcon.git"
  }
}
```

The Git dependency follows the mutable `main` branch. A committed submodule
gitlink already pins the exact package commit checked out in the parent project.

## Quick Start

1. Assign an application icon in Player Settings. The active build target's
   first assigned icon is preferred.
2. Open **Preferences > Essentials > Project Icon**, verify Auto Apply, and
   choose the appearance you want. Auto Apply defaults on when no earlier
   preference exists.
3. Turn off **Auto Apply Player Icon** whenever you want Unity's normal
   application icon back.

There is no manual Apply command. The package reapplies after startup, project
changes, focus changes, and periodic maintenance.

## Appearance

- **Badge** places the Player icon in the upper-left of Unity's icon.
- **Icon Replacement** uses the Player icon across the complete application
  icon.

If the active build target has no assigned icon, Project Icon falls back to the
Unknown target group and then to the first icon found in another target group.
If no Player icon exists anywhere, the current process icon is left as-is. Turn
off Auto Apply to reset a previously applied custom icon.

## Compatibility and Opt-Outs

Use `ESSENTIALS_DISABLE_PROJECT_ICON` to disable the managed product and test
assemblies without removing the package. The native bundle remains installed.
The established `ESSENTIALS_DISABLE_EDITOR_ICON` and `ESSENTIALS_DISABLE_ALL`
symbols have the same effect.

Turn off Auto Apply before removing or compiling out the package so the current
Editor process resets its icon.

The managed assembly, native bundle, preference keys, cache path, exported
entry points, and Unity asset GUIDs retain their established values.

## Documentation

- [Start here](Documentation~/index.md)
- [User guide](Documentation~/user-guide.md)
- [Compatibility](Documentation~/compatibility.md)
- [Troubleshooting](Documentation~/troubleshooting.md)
- [Native bundle notes](Documentation~/native-bundle.md)
- [Changelog](CHANGELOG.md)

## License

This package is proprietary. See [LICENSE.md](LICENSE.md) for the applicable
terms.
