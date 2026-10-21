# Build, test, and package APK Runner

## Prerequisites

Use **Windows x64**, **Windows PowerShell 5.1**, Git, and a .NET SDK that supports SDK-style .NET Framework projects. This checkout was validated with **.NET SDK 10.0.402**. The app targets **.NET Framework 4.8**, not .NET 10: the SDK is the build tool.

The project restores `Microsoft.NETFramework.ReferenceAssemblies` from NuGet. Regression scripts also use Windows' Framework compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

Internet is needed for the first NuGet/tool restore. Android SDK images, Java and scrcpy are **not needed to compile or run fixture checks**. Download them only when you intend to test real Android behavior.

If Windows blocks a reviewed local script because of execution policy, you can run that command with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script>`. This override applies only to that PowerShell process; a permanent machine-wide policy change is not required.

## 1. Build

```powershell
git clone https://github.com/MiMat82/APK-Runner.git
Set-Location APK-Runner
powershell.exe -NoProfile -File .\Build-App.ps1
```

`Build-App.ps1` reads `release\VERSION.txt`, restores dependencies using `NuGet.Config`, and builds `src\APKRunner.csproj` in Release mode for x64. A custom `-Version` must use `major.minor.patch`.

Output is `artifacts\updater-app\APKRunner.exe`. Keep its dependency DLLs and notices beside it; copying only the EXE is insufficient. `Build-Preview.ps1` builds the same maintained source into `artifacts\preview`. The `src\preview` directory name is historical.

## 2. Run a development build deliberately

An unmanaged executable can reuse the registered installed runtime when it has no adjacent `runtime` folder. Separate executable output alone does **not** isolate Android data. To create an isolated development runtime first:

```powershell
New-Item -ItemType Directory -Path .\artifacts\updater-app\runtime -Force
Start-Process -FilePath .\artifacts\updater-app\APKRunner.exe
```

The empty runtime needs its own setup/downloads before launching Android apps. Use disposable apps/data. Do not attach a production virtual disk to multiple emulator processes. An unmanaged build does not enable the installed-release updater merely by starting it.

## 3. Regression checks

Build first to provide the updater dependency DLLs, then run:

```powershell
powershell.exe -NoProfile -File .\tests\Run-Checks.ps1
powershell.exe -NoProfile -File .\tests\Run-UpdateChecks.ps1
powershell.exe -NoProfile -File .\tests\Run-StopChecks.ps1
powershell.exe -NoProfile -File .\tests\Run-SigningChecks.ps1
powershell.exe -NoProfile -File .\tools\Verify-Branding.ps1
```

The main suite covers storage, guarded cleanup, English UI/resource preferences, UI rendering, launch arguments, asynchronous startup and configuration helpers. Cleanup tests use synthetic files and a fixture-local junction. Update checks cover paths, policy and sizing. Stop checks use fake ADB child processes to verify both managed devices, identity refusal, locking and data preservation. Signing checks use test records; passing them does not establish that a real certificate exists or a package is signed.

Fixtures and evidence go under ignored `analysis`. Preserve them for debugging; do not publish real runtime diagnostics blindly.

### Optional local updater integration

If the pinned packager is not already installed by `Build-Release.ps1`:

```powershell
dotnet tool install vpk --version 1.2.161 --tool-path .\.tools\velopack --configfile .\NuGet.Config
```

Then run:

```powershell
powershell.exe -NoProfile -File .\tests\Run-UpdateIntegration.ps1
```

This builds isolated 1.0.0/1.0.1 probe packages, starts hidden test processes, checks successful update/corrupt-package rejection, and checks a synthetic data hash. It does not install the main app or boot Android.

`Run-GitHubUpdateIntegration.ps1` is a separate **live-network** probe. Its `-ExpectedVersion` parameter defaults to `release\VERSION.txt`; use an explicit version when validating a particular published release:

```powershell
powershell.exe -NoProfile -File .\tests\Run-GitHubUpdateIntegration.ps1 -ExpectedVersion 12.11.0
```

This probe checks the real GitHub update feed, package download/integrity and application in an isolated installation. It uses synthetic data, does not start a real Android emulator, and does not test the GUI restart or post-update interaction with a real Android app. It is not part of the routine offline suite or a clean-machine Android test.

## 4. Unsigned local packaging

```powershell
powershell.exe -NoProfile -File .\tools\Build-Release.ps1 -AllowUnsignedDevelopment
```

This builds a fresh staging directory, installs pinned Velopack 1.2.161 if necessary, and creates a versioned folder under `artifacts`. It does not install or publish the result. The package audit rejects runtime folders, user data and PDB files.

| File | Purpose |
| --- | --- |
| `APKRunner-win-Setup.exe` | Windows installer. |
| `APKRunner-win-Portable.zip` | Launcher bundle; Android runtime/data remain separate. |
| `APKRunner-<version>-full.nupkg` | Updater package. |
| `releases.win.json`, `assets.win.json`, `RELEASES` | Update/packaging metadata. |
| `SHA256SUMS.txt` | Generated file checksums. |

Unsigned development packages can show publisher warnings. Building one does not authorize publication. The publishing helper requires signed packages by default; an unsigned public release needs the separate, explicit maintainer decision described below.

## 5. Official release publication

Release packaging defaults to requiring a Windows-trusted, timestamped code-signing identity. Copy the appropriate placeholder `release\signing.*.example.json` to ignored `release\signing.local.json`, then configure the actual issuer-validated identity. Windows certificate store and Azure Artifact Signing are supported.

Do not commit keys, real signing configuration or credentials. The scripts do not create a certificate or signing account. Review `Signing.ps1` and `Verify-SignedRelease.ps1` before configuring a provider.

For a new official binary release, update `release\VERSION.txt` and `release\NOTES.md`, run the checks, and build a fresh package. Include `LICENSE.txt` and `THIRD-PARTY-NOTICES.md`; Release PDBs, Android runtimes and user data must not enter the package. Review and publish the corresponding source commit before using its full 40-character hash as `-TargetCommit`:

```powershell
powershell.exe -NoProfile -File .\tools\Build-Release.ps1 -Version 12.11.0
$sourceCommit = (git rev-parse HEAD).Trim()
powershell.exe -NoProfile -File .\tools\Publish-Release.ps1 `
    -ReleaseDirectory '<reviewed release directory>' -Version 12.11.0 `
    -TargetCommit $sourceCommit
```

The default publication path verifies the actual packaged signatures and timestamps against the configured publisher. `Verify-ReleasePayload.ps1` also checks the release's expected files, checksums, update metadata, versions and permitted archive contents. To run that audit separately:

```powershell
powershell.exe -NoProfile -File .\tools\Verify-ReleasePayload.ps1 `
    -ReleaseDirectory '<reviewed release directory>' -Version 12.11.0
powershell.exe -NoProfile -File .\tests\Run-ReleasePayloadChecks.ps1
```

The helper uses GitHub CLI/credential-manager authentication, requires a source commit and refuses to overwrite an existing version. It creates a **draft**, downloads the uploaded assets, compares their hashes with the reviewed local files and audits the downloaded payload again. Only after those checks pass does it publish the release and mark it latest, exposing it to existing updaters. A failed upload/readback leaves the draft unpublished for inspection. Publishing binaries is not required to contribute source changes.

### Explicit unsigned release decision

An unsigned release does not provide a Windows-verified publisher. A maintainer can deliberately publish one when the owner has authorized that distribution choice. Build it with `-AllowUnsignedDevelopment`, disclose that it is **not Authenticode-signed** in `release\NOTES.md`, then pass both the publication switch and the actual reason:

```powershell
powershell.exe -NoProfile -File .\tools\Build-Release.ps1 `
    -Version 12.11.0 -AllowUnsignedDevelopment
$sourceCommit = (git rev-parse HEAD).Trim()
powershell.exe -NoProfile -File .\tools\Publish-Release.ps1 `
    -ReleaseDirectory '<reviewed release directory>' -Version 12.11.0 `
    -TargetCommit $sourceCommit -AllowUnsignedRelease `
    -UnsignedReleaseReason '<recorded owner-authorized reason>'
```

This choice bypasses only the requirement for a trusted publisher signature. It does not bypass payload validation, damaged-signature rejection, source tagging, draft upload readback or checksum comparison. Do not use it to describe an unsigned release as signed. The 12.11.0 release uses this explicit unsigned path; no signing subscription was purchased.

## 6. Fork configuration

The upstream URL is compiled into `src\preview\AppUpdates.cs`, and `tools\Publish-Release.ps1` targets `MiMat82/APK-Runner`. Change both before distributing a fork's update-enabled builds. Review package identity, app name, Windows registration identifiers, runtime paths and signing expectations to avoid installation collisions.

Do not unintentionally point a modified managed build at upstream releases: a detected update can replace it with an upstream binary. Unmanaged development builds have no Velopack update manager but can still read persistent settings. Use an adjacent development runtime for data isolation.

## Runtime and branding tools

Most runtime tools require explicit **`-RuntimeRoot`**. They are advanced helpers, not normal installation steps. Read parameters/code before modifying a real runtime. `Measure-RuntimeStorage.ps1` reports sizes; compression/optimization scripts can change files or settings.

`Review-Cleanup.ps1` is a **legacy-layout** helper and fixture dependency. It requires `-AppRoot` with the old `APKRunner V12.9.exe` and `runtime` layout, previews exact historical targets by default, and deletes only with `-Apply`. It is not a general cleaner. Do not create a dummy EXE to bypass its target check on real data.

`Prepare-Preview.ps1` checks/repairs AVD pointers against an explicit runtime. `Set-FastRendering.ps1` writes a machine-specific preference. `Repair-ShellIcons.ps1` operates on an installed launcher. These are separate from building source.

Artwork is embedded from `assets\branding`. To replace your project's artwork:

```powershell
powershell.exe -NoProfile -File .\tools\Import-Branding.ps1 -Source C:\Artwork\my-icon.png
powershell.exe -NoProfile -File .\Build-App.ps1
powershell.exe -NoProfile -File .\tools\Verify-Branding.ps1
```

The importer overwrites project branding assets and regenerates the ICO. The README banner is separate; see [ARTWORK.md](ARTWORK.md).

## Verification limits

See [VERIFICATION.md](VERIFICATION.md). Compilation, fixture checks, package integration and readiness checks do not prove Android app compatibility on another PC. Report real app and clean-machine testing separately.
