# Third-party components

The MIT License covers APK Runner's own code, documentation, and project artwork. It does not relicense software downloaded by the installer or apps supplied by users.

## Launcher dependencies

| Component | Use | License information |
| --- | --- | --- |
| Velopack 1.2.161 | Windows packaging and automatic updates; restored from NuGet. | MIT; [included notice](licenses/Velopack-LICENSE.txt), [upstream](https://github.com/velopack/velopack). |
| Newtonsoft.Json | Transitive updater dependency distributed beside the launcher. | MIT; [included notice](licenses/Newtonsoft.Json-LICENSE.txt), [upstream](https://github.com/JamesNK/Newtonsoft.Json). |
| Microsoft.NETFramework.ReferenceAssemblies 1.0.3 | Build-time reference assemblies. | Distributed through NuGet under the package's terms. |
| .NET Framework 4.8 | Windows launcher runtime. | Microsoft runtime terms; not relicensed by this project. |

## Components downloaded during setup

These are not committed to the repository. Review upstream terms and notices delivered with each downloaded package.

- **Eclipse Temurin Java 17 JRE:** resolved through the [Adoptium API](https://adoptium.net/). Retain the notices supplied with the JRE; OpenJDK-derived components commonly use GPLv2 with the Classpath Exception, with additional component notices.
- **Android SDK command-line tools, Platform Tools, Build Tools, Emulator, and Google APIs / Android TV system images:** downloaded through Google's repository and SDK manager. The app links to the [Android SDK terms](https://developer.android.com/studio#terms) and requires consent before setup. Different packages/images can have different terms. MIT licensing of APK Runner does not remove these obligations.
- **scrcpy 4.1:** downloaded from the [official Genymobile release](https://github.com/Genymobile/scrcpy/releases/tag/v4.1). scrcpy uses Apache-2.0; the Windows distribution includes dependencies with their own notices. Preserve and review those notices.

User-provided APKs, split APKs, and OBB files belong to their authors and are not supplied or licensed by APK Runner.

If redistributing a complete runtime or modified binary package, review the exact components included. The source repository is not a license grant for external components.
