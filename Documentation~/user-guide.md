# User Guide

## Choose the Player Icon

Project Icon looks for the first assigned icon in this order:

1. Application or general icon for the active build-target group;
2. icon assigned to `BuildTargetGroup.Unknown`;
3. first icon found in another build-target group.

Assigning an icon for the active target gives the least surprising result. If
no Player icon is assigned anywhere, the current process icon is left as-is.
That can include an icon applied earlier in the session. Turn off Auto Apply to
reset it.

When the source icon is a normal asset file, the package passes that file to the
native bundle. If AppKit cannot decode that file, or Unity exposes only an
in-memory texture, the package writes a readable PNG copy to:

```text
Library/Essentials/EditorIcon/EditorIcon.png
```

That cache belongs to the local project Library and is not a source asset.

## Configure the Appearance

Open **Preferences > Essentials > Project Icon**.

### Auto Apply Player Icon

Auto Apply defaults on when no earlier preference exists. On macOS, the package
applies the icon after load and keeps checking because Unity can restore its own
application icon during normal Editor activity.

Turning Auto Apply off resets the current process to Unity's normal icon on the
next Editor update. It does not delete the Library cache.

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

- `Essentials.EditorIcon.AutoApplyPlayerIcon`
- `Essentials.EditorIcon.ApplicationIconMode`

They are user-global rather than project files. Choosing Icon Replacement in
one project therefore becomes the preferred mode when another project opens
under the same user account.

## Reset to Unity's Icon

Turn off **Auto Apply Player Icon**. There is intentionally no legacy
**Tools > Essentials > Editor Icon** menu and no manual Apply or Reset command.

Re-enable Auto Apply to use the current project's selected Player icon again.
Turn Auto Apply off before removing the package or adding a compile-disable
symbol. If the assembly is removed during a reload while a custom icon remains,
restart Unity to restore the normal process icon.
