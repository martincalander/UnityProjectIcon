# Troubleshooting

## The Dock Icon Does Not Change

Check the basics first:

1. Confirm the Editor is running on macOS.
2. Assign at least one application icon in Player Settings.
3. Open **Preferences > Project Icon** and confirm **Auto Apply
   Player Icon** is on.
4. Bring the Editor back into focus and allow a moment for the next apply pass.

There is no manual Apply button. Auto Apply is the only user workflow.

## The Wrong Player Icon Is Used

Project Icon prefers the active build target, but it deliberately falls back to
`BuildTargetGroup.Unknown` and then other target groups. Assign an application
icon explicitly for the active build target when several platform icon sets are
present.

Within a target group, the first assigned Application or general icon is used.

If every Player icon is removed, Project Icon leaves the current process icon
as-is rather than resetting it. Turn off Auto Apply to restore Unity's icon.

## The Icon Returns to Unity's Default

Unity can restore its own application icon during startup and normal Editor
activity. Project Icon responds to focus and project changes and also performs
periodic maintenance.

If it continues reverting, confirm Auto Apply is still enabled and that the
native bundle is present at:

```text
Editor/Plugins/macOS/ProjectIcon.bundle
```

## The Preference Changed in Another Project

This is expected. Project Icon uses user-global `EditorPrefs` rather than a
project settings asset. Auto Apply and Appearance are shared by Unity projects
opened under the same operating-system user.

## Windows or Linux Shows a Warning

The Preferences page is cross-platform, but icon application is macOS-only.
Windows and Linux save the choices and make no icon changes. See
[Compatibility](compatibility.md).

## A Source Icon Cannot Be Read

The package normally reads the icon asset directly. If it needs a readable
copy, it writes:

```text
Library/com.martincalander.projecticon/ProjectIcon.png
```

Check that the project Library is writable. Deleting that generated file is
safe while Unity is closed; Project Icon recreates it when needed.

## The Native Bundle Fails to Load

The package has no diagnostic panel and does not log silent auto-apply
failures. Verify that the checked-in bundle is intact, imported for the macOS
Editor, and contains a slice for the current CPU.

Developers rebuilding the bundle should follow
[Native bundle notes](native-bundle.md) and preserve its identifier, file name,
entry points, architectures, and Unity asset GUID.

## The Icon Remained After Removing or Disabling the Package

There is no unload hook after the managed assembly has gone. Turn off Auto Apply
before removing the package or adding a compile-disable symbol. If that is no
longer possible, restart Unity to restore the normal process icon.
