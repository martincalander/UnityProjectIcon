# Project Icon Documentation

Project Icon applies a project's Player icon to the running Unity Editor on
macOS. It changes only the current process, not the Unity installation, project
assets, or Player builds.

## Guides

- [Installation](../README.md#installation)
- [User guide](user-guide.md)
- [Compatibility](compatibility.md)
- [Troubleshooting](troubleshooting.md)
- [Native bundle notes](native-bundle.md)
- [Changelog](../CHANGELOG.md)

After installation, assign an application icon in Player Settings, then verify
the user-global options under **Preferences > Project Icon**.

The package has no runtime assembly or scene component. Windows and Linux load
and save the settings but make no native icon call.

## License

This package is proprietary. See [LICENSE.md](../LICENSE.md).
