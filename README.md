# APK Runner

This public repository distributes **Windows x64 releases** of APK Runner. The application source code is maintained privately.

[Download the latest release](https://github.com/MiMat82/APK-Runner/releases/latest)

Choose **APKRunner-win-Setup.exe** for the installer. The automatically generated source archives contain this download catalog, not the application source or installer.

## Updates

Installed versions check this repository's stable releases. When a newer stable release is found, APK Runner shows a notification and an **Update and restart** action. Install the required update before starting additional Android apps. Save your work and stop Android before applying it; the updater does not stop active Android sessions for you.

A network error alone does not mean an update exists. If a required newer version was already discovered, that requirement remains until a successful check or update resolves it.

## Existing apps and saved data

Android runtimes and user data are stored separately from the replaceable Windows launcher. During first setup on an existing installation, select the original APK Runner folder or its `runtime` folder when prompted. Do not delete that folder. An ordinary launcher update does not include or replace Android data disks.

New users can install the Android runtime from the app after accepting the Android SDK terms. Emulator use requires compatible Windows x64 hardware and virtualization support.

## Release files

- `APKRunner-win-Setup.exe`: Windows installer.
- `APKRunner-win-Portable.zip`: launcher bundle; Android data remains external to its update-managed folder.
- `APKRunner-<version>-full.nupkg` and `releases.win.json`: automatic-update files; leave their names intact when publishing.
- `SHA256SUMS.txt`: download checksums.

The first update-enabled release is not Authenticode-signed. Windows may display a publisher warning. Update packages undergo integrity checks; publisher signing is a separate distribution step.

No installed Android apps, saved data, Android system images, access tokens or private source code are published in this repository.
