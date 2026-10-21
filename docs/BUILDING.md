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
powershell.exe -NoProfile -File .\tests\Run-SigningChecks.ps1
powershell.exe -NoProfile -File .\tools\Verify-Branding.ps1
```

The main suite covers storage, guarded cleanup, UI rendering, launch arguments and configuration helpers. Cleanup tests use synthetic files and a fixture-local junction. Update checks cover paths, policy and sizing. Signing checks use test records; passing them does not establish that a real certificate exists or a package is signed.

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

`Run-GitHubUpdateIntegration.ps1` is a separate **live-network** probe that downloads an upstream release and currently expects **12.10.0**. Review that expectation if upstream changes. It is not part of the routine offline suite or a clean-machine Android test.

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

Unsigned development packages can show publisher warnings. The publishing helper rejects them. The existing public 12.10.0 release predates the stricter signing gate.

## 5. Signed releases

Release packaging defaults to requiring a Windows-trusted, timestamped code-signing identity. Copy the appropriate placeholder `release\signing.*.example.json` to ignored `release\signing.local.json`, then configure the actual issuer-validated identity. Windows certificate store and Azure Artifact Signing are supported.

Do not commit keys, real signing configuration or credentials. The scripts do not create a certificate or signing account. Review `Signing.ps1` and `Verify-SignedRelease.ps1` before configuring a provider.

For a new official binary release, maintainers update `release\VERSION.txt` and `release\NOTES.md`, build/validate the signed package, then explicitly run the publishing helper. It uses GitHub CLI/credential-manager authentication and refuses to overwrite an existing version. Publishing binaries is not required to contribute source changes.

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
