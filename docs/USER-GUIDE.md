# APK Runner user guide

APK Runner opens local Android APK and XAPK packages in Windows windows. It manages an Android Emulator runtime and uses scrcpy for the app window, mouse, keyboard and playback audio.

The interface currently uses Danish labels. This guide includes the labels you will see and their English meanings.

## Before you start

- Use an **Intel/AMD x64 PC running Windows 10 22H2 or Windows 11**. Windows on ARM and 32-bit Windows are not supported by the current platform check.
- The application targets **.NET Framework 4.8**. The release installer declares this dependency; the .NET SDK is only needed when building the source.
- Hardware virtualization and a working emulator hypervisor are required. The application checks acceleration after the emulator is installed. Your PC may need virtualization enabled in firmware and **Windows Hypervisor Platform** enabled in Windows, followed by a restart.
- Allow time, internet access and free disk space for the first Android runtime download. The small Windows installer does **not** contain the Android system images. The runtime and installed Android apps use substantially more space than the launcher.
- There is no published, validated minimum RAM or disk-space figure. Runtime storage grows with installed apps, their saved data, additional compatibility images and snapshots. The profiles adapt their Android RAM allocation to the host, but that does not establish a tested minimum specification.
- Obtain the APK/XAPK files you want to use separately. APK Runner does not supply Android apps or an app store.

An APK is executable software. Use packages from sources you trust. Compatibility depends on the particular app; APK Runner does not guarantee that every Android app or game will work.

## 1. Install APK Runner

1. Open [APK Runner Releases](https://github.com/MiMat82/APK-Runner/releases/latest).
2. Download **APKRunner-win-Setup.exe** from the release assets.
3. Run the installer and open **APK Runner**.

The currently published release may differ from the source in the repository. Read its release notes for changes and distribution details. A package integrity hash does not establish a verified Windows publisher; check the release notes for signing status.

### First launch: new installation

When asked whether you already have APK Runner with installed apps or saved data, select **No**. APK Runner creates a new runtime location outside the application files that its updater replaces.

Choose **Normal** when the performance-profile dialog appears. You can try **Høj ydeevne** (High performance) later if your PC has enough resources.

### First launch: reuse an existing installation

If you already have Android apps or saved data in an older APK Runner installation:

1. Select **Yes** when the first-launch dialog asks about an existing installation.
2. Select the original APK Runner folder containing `runtime`, or select that `runtime` folder directly.
3. Keep that folder in place. The new installation registers its location and reuses it; this is not a copy or backup of your data.

The selected runtime must contain an `avd` directory. Do not select the new updater-managed application folder as the runtime location. **Cancel** exits this setup without selecting a new runtime.

Once a runtime has been registered, this question normally does not appear again. If a registered runtime later becomes unavailable, restore access to the original folder rather than creating an empty replacement and expecting the old apps to appear there.

## 2. Install the Android runtime

1. Let the initial component check finish.
2. Select **Læs vilkår** (Read terms) and read the Android SDK terms.
3. If you accept them, tick **Jeg accepterer Android SDK-vilkårene** (I accept the Android SDK terms).
4. Select **Installér runtime** (Install runtime).
5. Wait while the activity log reports download and installation progress. The application downloads its private Java runtime, Android SDK tools, Android Emulator, system image and scrcpy as needed.
6. Use **Kontrollér** (Check) to refresh component status. Components must show **KLAR** (Ready) before a new app can launch. **MANGLER** means a component or requirement is missing.

The main runtime uses **Android 15 / API 35, Google APIs, x86_64**. For an eligible legacy 32-bit x86 package, APK Runner installs a separate **Android 14 / API 34, Android TV, x86** runtime on demand. That additional download needs internet access and more disk space. The TV image is a compatibility path; it is separate from choosing landscape orientation.

Android SDK and other downloaded components have their own licenses and terms. The repository's license for APK Runner does not replace those terms or grant rights to third-party Android apps.

If installation stops, read the last messages under **AKTIVITETSLOG** (Activity log). Resolve the reported issue, then check or retry installation. Do not delete the runtime to fix a missing component if it contains apps or saved data you want to keep.

## 3. Open your first app

1. Under **APPVINDUE** (App window), choose:
   - **Liggende / landscape** for a wide window.
   - **Stående / portrait** for a tall window.
2. Select **Åbn APK / XAPK** (Open APK / XAPK).
3. Select one `.apk` or `.xapk` file. You can also drop a single package onto the APK Runner window.
4. Wait for Android to boot and for the package to install. The first boot, or a cold boot, can take several minutes.
5. Use the app window with your mouse and keyboard. Audio playback is handled through scrcpy and depends on the Android app and runtime.

APK Runner reads the package's native libraries and chooses a compatible runtime. If the same package version is already installed in that runtime, it skips reinstallation and opens the installed app. A different version is installed with Android's replace option; Android can still reject the replacement because of signing, downgrade or compatibility rules.

XAPK handling installs the contained APK files together and copies supported `Android/obb/<package>/*.obb` expansion files. An XAPK must contain a valid, compatible package set. `.aab`, `.apks` and `.apkm` are not accepted input formats.

### Open the app again

After a successful launch, APK Runner attempts to create a Windows desktop shortcut named after the selected package file. You can also reopen the original package through **Åbn APK / XAPK**.

**Keep the original APK/XAPK at the same path.** The desktop shortcut passes that file's path to APK Runner; moving or deleting the file breaks that shortcut even if the Android app is still installed. Reopen the file from its new location to create an updated shortcut.

### Enable double-click opening

Select **Aktivér dobbeltklik** (Enable double-click) to register APK Runner for `.apk` and `.xapk` files for your Windows account. If Windows retains another default app, use **Open with** in File Explorer and select APK Runner.

## 4. Orientation and performance

Choose portrait or landscape **before opening a package**. Changing the selector does not immediately rotate an already-open app window; reopen the app using the desired choice.

The main runtime creates a 720 × 1280 portrait or 1280 × 720 landscape virtual display. The legacy x86 runtime changes its shared primary display, so its orientation choice affects other apps open on that same virtual device. An Android app can still impose its own orientation requirements.

At startup, choose a performance profile:

| Profile | What it changes |
| --- | --- |
| **Normal** | Uses fewer Android CPU cores and less RAM than High performance on hosts with sufficient resources. Start here. |
| **Høj ydeevne** | Assigns more Android CPU cores and RAM when the host has enough resources. It can leave less capacity for other Windows applications. |

To choose a different profile, restart APK Runner and select it in the startup dialog. Applying a different profile to a running virtual device can restart Android, so save work first.

Some previously configured runtimes expose **Normal / 30 FPS** and **Høj ydeevne / op til 60 FPS**. These labels depend on an optional, machine-specific rendering setting; they are not the standard configuration for every fresh installation. Frame-rate limits are targets or caps, not a guarantee that a particular app will reach them.

## 5. Close apps and stop Android

Save your work inside the Android app before closing it. Closing the app's Windows window does **not** shut down the Android emulator; Android stays running in the background.

**Stop Android** stops APK Runner's main phone virtual device. **Current limitation:** it does not stop the separate legacy x86/TV virtual device. Closing APK Runner itself also does not automatically stop either emulator.

If the legacy runtime is still running, save its work and shut it down before applying an APK Runner update. Technical users can stop only the identified APK Runner TV device with its bundled ADB:

```powershell
# For an installed release with a registered runtime:
$registration = Join-Path $env:LOCALAPPDATA 'APKRunnerData\runtime-path.txt'
$runtimeRoot = (Get-Content -LiteralPath $registration -Raw).Trim()
$adbPath = Join-Path $runtimeRoot 'android-sdk\platform-tools\adb.exe'
$deviceName = & $adbPath -s emulator-5582 emu avd name
if ($LASTEXITCODE -eq 0 -and $deviceName -contains 'APKRunnerTV') {
    & $adbPath -s emulator-5582 emu kill
} else {
    Write-Error 'APKRunnerTV was not identified. No device was stopped.'
}
```

The name check is important: do not stop an unrelated emulator just because it occupies a particular port. If your launch mode has no registered runtime, use the actual APK Runner runtime path instead of the registration-file lookup.

## 6. Updates

Installed releases check the repository's stable GitHub releases at startup, every six hours while open, and when you select **Søg efter opdateringer** (Check for updates).

When a newer stable release is discovered:

1. Save work in your Android apps.
2. Close the app windows and stop the APK Runner Android runtimes. Remember the legacy-device limitation above.
3. Select **Opdatér og genstart** (Update and restart).
4. Wait for the verified package to download, install and restart APK Runner.

A discovered newer stable release is required before starting additional Android apps. Already-running Android sessions continue until you close them. A failed network check alone does not create an update requirement, but an already-discovered required version is cached and can continue to block launches while you are offline.

Updates replace the Windows application separately from the Android runtime. That separation is intended to preserve installed Android apps and saved data. It does not replace a backup, and it does not update your Android APKs automatically. The source-tree build and other unmanaged launches do not use the installed-release updater in the same way.

## 7. Storage, data and backups

Select **Diskforbrug** (Disk usage) to view the runtime path, logical file sizes and Windows-reported occupied disk space. Compression and sparse virtual disks can make logical size differ from space used on disk.

The report lists possible cleanup candidates, but the report itself does **not** delete them. The checkbox **Spar snapshot-plads ved næste Android-start (kold opstart)** means “Save snapshot space at the next Android start (cold boot).” It changes how the next emulator process starts; it does not immediately reclaim all existing snapshot files or uninstall apps. Cold boot can take longer.

For a normal installed release:

- Runtime registration and persistent settings are under `%LocalAppData%\APKRunnerData`.
- `runtime-path.txt` identifies the actual Android runtime location. A new installation normally uses `%LocalAppData%\APKRunnerData\runtime`; an adopted installation keeps its original location.
- The activity log is also written to `APKRunner.log` inside that runtime.

To preserve a complete local setup, save Android work, shut down both virtual devices and close APK Runner, then back up the actual runtime folder and persistent configuration. Back up your original APK/XAPK files as well if you use shortcuts. Virtual disks can be large. A registration file or Windows shortcut alone does not contain your Android apps or their data.

Before sharing logs or screenshots for support, review them for local paths, filenames or Android package names you do not want to publish.

## Troubleshooting

| What you see | What to check |
| --- | --- |
| **Åbn APK / XAPK** is unavailable | Wait for the current operation to finish. Use **Kontrollér** and resolve every **MANGLER** entry. Also check whether a required update is blocking new launches. |
| **Installér runtime** is unavailable | Read the SDK terms and select the acceptance checkbox if you agree. Controls are also temporarily disabled during other operations. |
| Virtualization is missing | Finish installing the emulator so it can perform its acceleration check. Check firmware virtualization and Windows Hypervisor Platform, restart Windows when required, then use **Kontrollér**. |
| A download or runtime installation fails | Read the activity log, check internet access and available disk space, then retry. Installation depends on upstream download services and their available packages. |
| The first launch is slow | Android may be cold-booting and performing background initialization. Wait for the activity log. Repeatedly launching the same package or force-closing the emulator can make diagnosis harder. |
| Android fails to boot or the app window closes immediately | Read the activity log for emulator graphics, acceleration or scrcpy errors. Confirm that the component check passes and avoid running another APK Runner operation at the same time. |
| A message says the Android port belongs to another emulator | Close or reconfigure the conflicting emulator. APK Runner uses dedicated ports 5580 and 5582 and verifies the virtual-device name before reusing them. |
| A package has only 32-bit ARM libraries | The current version rejects ARM32-only native apps. Obtain a compatible x86, x86_64 or arm64-v8a build from the app's publisher if available. |
| Android reports an installation failure | Check that the package is complete, targets a supported Android environment and has a compatible signature/version. Do not rename another bundle format to `.xapk`; that does not convert it. |
| Installation succeeds but the app has no launchable activity | The package may be a service, plugin or component rather than an app with a launchable screen. APK Runner cannot open it as a normal app window. |
| Portrait or landscape appears ignored | Select the orientation before opening the package. The app can enforce its own orientation. Legacy x86 apps share a display orientation. |
| A desktop shortcut no longer works | Restore the original APK/XAPK path or open the package from its new location to recreate the shortcut. |
| Double-click opens a different Windows program | Use File Explorer's **Open with** and select APK Runner after enabling its file registration. |
| An update says Android is still running | Close app windows and stop the runtimes. **Stop Android** currently handles only the main phone device; check the legacy-device instructions above. Closing a window alone does not shut Android down. |
| A required update blocks launches while offline | Reconnect and install the discovered required release. A cached update requirement remains active during a network outage. |
| Previously installed apps appear missing | Check the actual runtime path. Different runtimes have separate app/data storage. Restore access to your registered original runtime rather than deleting it or starting over. |

If you need help, open an [issue](https://github.com/MiMat82/APK-Runner/issues) with the APK Runner release version, Windows version, package format, component status, relevant activity-log messages and exact steps to reproduce. Describe the Android app and architecture if known. Share APKs or private app data only when you have permission and intend them to be public.

## Compatibility boundaries

- x86_64 native apps use the main x86_64 runtime. ARM64-only native apps use its translation path; translation support does not guarantee successful operation or performance for every ARM64 app.
- Eligible 32-bit x86 native apps use the separate Android TV/x86 image. This is not a guarantee of compatibility with every phone app.
- ARM32-only native apps are unsupported. Apps without native libraries are not rejected by the native-library check, but still have their own Android requirements.
- Apps can depend on services, hardware, DRM, device integrity checks, input methods or graphics behavior unavailable in these virtual devices.
- The primary Google APIs image is not a promise that every Google-dependent feature or Play Store workflow will work.
- Installation, a ready component table and a visible app window are different levels of success. Verify the features and saved-data behavior that matter for your particular app.

Return to the [repository overview](../README.md).
