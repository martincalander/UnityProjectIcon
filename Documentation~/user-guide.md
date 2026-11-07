# User Guide

## Choose the Player Icon

Project Icon looks for the first assigned icon in this order:

1. Application or general icon for the active build-target group;
2. icon assigned to `BuildTargetGroup.Unknown`;
3. first icon found in another build-target group.

Assigning an icon for the active target gives the least surprising result. If
no Player icon is assigned anywhere, an icon previously applied by Project Icon
is reset on the next check. Icons owned by other tools are left alone.

When the source icon is a normal asset file, the package passes that file to the
native bundle. If AppKit cannot decode that file, or Unity exposes only an
in-memory texture, the package writes a readable PNG copy to:

```text
Library/com.martincalander.projecticon/ProjectIcon.png
```

That cache belongs to the local project Library and is not a source asset.
Unchanged textures reuse their PNG copy. Texture updates, deleting the copy, or
overwriting it trigger regeneration. Unreadable, multichannel HDR, compressed,
and two-channel RG textures need a graphics device for conversion; ordinary source
image files can be used without that conversion. Two-channel conversion matches
Unity's displayed colors instead of adding a blue channel during PNG encoding.

## Configure the Appearance

Open **Preferences > Project Icon**.

### Auto Apply Player Icon

Auto Apply defaults on when no earlier preference exists. On macOS, the package
applies the icon after load and keeps checking because Unity can restore its own
application icon during normal Editor activity.

Turning Auto Apply off resets an icon owned by Project Icon on the next Editor
update. Enabling it requests an apply on the next update, even if a maintenance
check just ran. Neither action deletes the Library cache.

### Appearance

| Mode | Result |
| --- | --- |
| **Badge** | Draws the Player icon at the upper-left of Unity's icon |
| **Icon Replacement** | Draws the Player icon across the full application icon |

Badge is the first-run default and keeps Unity recognizable when several
applications share the Dock.

Appearance changes apply immediately on macOS. The package also reapplies after
project and focus changes and performs a slower maintenance check while the
Editor remains open.

## Understand Preference Scope

The settings are stored in the operating-system user's Unity `EditorPrefs`:

- `com.martincalander.projecticon.AutoApplyPlayerIcon`
- `com.martincalander.projecticon.ApplicationIconMode`

They are user-global rather than project files. Choosing Icon Replacement in
one project therefore becomes the preferred mode when another project opens
under the same user account.

## Reset to Unity's Icon

Turn off **Auto Apply Player Icon**. There is no manual Apply or Reset command.

Re-enable Auto Apply to use the current project's selected Player icon again.
Removing the package or adding the compile-disable symbol releases its callbacks,
native notification observers, and pinned icon when Unity unloads the code.
Ordinary script reloads reset the owned icon and reapply it after initialization.
