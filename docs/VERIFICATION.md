# Open-source publication verification

Checked on **2026-10-21** on a Windows x64 development PC using **.NET SDK 10.0.402** and Windows PowerShell 5.1.

## Results

| Check | Result |
| --- | --- |
| Build maintained launcher from the public checkout | Passed; zero warnings and errors. |
| Main runtime/storage/UI checks | 58 passed. |
| Guarded cleanup fixture assertions | 14 passed; synthetic user-data hashes and outside-junction target preserved. |
| Update policy/path/window checks | 25 passed. |
| Signing policy fixtures | 11 passed. |
| Local package integration | 2 scenarios passed: corrupt package rejected; valid update applied and restarted. Synthetic saved-data hash unchanged. |
| Embedded branding | Seven ICO sizes have transparent corners; embedded PNG and ICO match source artwork. |
| Unsigned development packaging | Passed; seven release assets generated. Package audit excludes Android runtimes, app data and PDB files. |
| Public export review | Maintained source/build/tests/artwork included; generated outputs, historical analysis, downloaded runtimes, signing credentials and personal app data excluded. |

That is **110 assertions/scenarios**, plus branding and build/package checks. The optional isolated updater probe compiles with an unused-field warning; the main application build has zero warnings.

README banner SHA-256: `3a41123dc0d9ee71e39928315e802de7f9f709d9943f02073db910c6b2fd9b23`.

## Publication changes

- Published the maintained source under MIT with dependency notices and detailed user/developer guides.
- Added a generated README banner based on the existing application artwork.
- Removed private workstation defaults from exported maintenance scripts and required explicit runtime/image paths.
- Made packaging use a fresh build directory, suppress Release PDB output, and create its ignored report directory.
- Added project/dependency notices to the build output.
- Fixed fixture junction cleanup for Windows PowerShell 5.1 using a checked fixture path and nonrecursive junction deletion.

## Limits

The source checkout was built without copying an Android runtime or private historical analysis into it. NuGet/tool caches on the development PC were available; this is not proof of an uncached clean-machine install.

No real Android app, emulator workload, firmware virtualization configuration, physical device, or independent PC was tested during the publication task. Android app compatibility and performance must be validated separately. No real Android data was reset or cleaned by these checks.

No new binary release is published as part of this source publication. Existing 12.10.0 release assets remain the earlier, unsigned downloads. The current source contains maintained changes and is not asserted to reproduce those assets byte for byte.

GitHub Actions is not configured as part of this publication. The source includes the local build and check commands; the results above are local validation, not hosted CI results.
