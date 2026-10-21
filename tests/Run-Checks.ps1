$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $project ('analysis\tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $testRoot 'PreviewChecks.exe'
$dependencies = Join-Path $project 'artifacts\updater-app'
foreach ($dependency in @('Velopack.dll', 'Newtonsoft.Json.dll')) {
    Copy-Item -LiteralPath (Join-Path $dependencies $dependency) -Destination $testRoot
}
& $compiler /nologo /codepage:65001 /target:exe /platform:x64 /optimize+ /main:APKRunner.PreviewChecks "/out:$testExe" /reference:System.Xml.Linq.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.IO.Compression.dll "/reference:$dependencies\Velopack.dll" "/reference:$dependencies\Newtonsoft.Json.dll" "$project\src\preview\*.cs" "/win32icon:$project\assets\branding\apk-runner.ico" "/resource:$project\assets\branding\apk-runner-logo.png,APKRunner.BrandLogo.png" "/resource:$project\assets\branding\apk-runner.ico,APKRunner.BrandIcon.ico" "$PSScriptRoot\PreviewChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
& $testExe $testRoot
if ($LASTEXITCODE -ne 0) { throw 'Preview checks failed' }

function Assert-Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    Write-Output "PASS $Message"
}
function Make-Fixture([string]$Name) {
    $fixture = Join-Path $testRoot $Name
    $paths = @(
        'runtime\backups\AVD-before-googleapis-20260910182744\old-data',
        'runtime\avd\APKRunner.avd\snapshots\default_boot\ram.img',
        'runtime\avd\APKRunnerTV.avd\snapshots\default_boot\ram.img',
        'runtime\android-sdk\system-images\android-35\default\x86_64\system.img',
        'runtime\android-sdk\system-images\android-35\google_apis\x86_64\system.img',
        'runtime\android-sdk\system-images\android-34\android-tv\x86\system.img',
        'runtime\avd\APKRunner.avd\userdata-qemu.img.qcow2',
        'runtime\avd\APKRunnerTV.avd\userdata-qemu.img.qcow2',
        'APKRunner V12.9.exe'
    )
    foreach ($relative in $paths) {
        $target = Join-Path $fixture $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        [IO.File]::WriteAllText($target, 'Fixture: ' + $relative)
    }
    Set-Content -LiteralPath (Join-Path $fixture 'runtime\avd\APKRunner.avd\config.ini') -Value 'image.sysdir.1 = system-images\android-35\google_apis\x86_64\'
    return $fixture
}
$clean = Join-Path $project 'tools\Review-Cleanup.ps1'
$fixture = Make-Fixture 'cleanup'
$preview = (& $clean -AppRoot $fixture | Out-String | ConvertFrom-Json)
Assert-Check ($preview.Targets.Count -eq 4) 'Four exact cleanup targets, preview only'
Assert-Check (Test-Path -LiteralPath $preview.Targets[0].AbsolutePath) 'Dry run preserves targets'
$protected = @(
    'runtime\avd\APKRunner.avd\userdata-qemu.img.qcow2',
    'runtime\avd\APKRunnerTV.avd\userdata-qemu.img.qcow2',
    'runtime\android-sdk\system-images\android-35\google_apis\x86_64\system.img',
    'runtime\android-sdk\system-images\android-34\android-tv\x86\system.img'
)
$hashes = @{}
foreach ($relative in $protected) { $hashes[$relative] = (Get-FileHash -LiteralPath (Join-Path $fixture $relative)).Hash }
& $clean -AppRoot $fixture -Apply | Out-Null
foreach ($target in $preview.Targets) { Assert-Check (!(Test-Path -LiteralPath $target.AbsolutePath)) "Fixture cleanup removed $($target.RelativePath)" }
foreach ($relative in $protected) { Assert-Check ((Get-FileHash -LiteralPath (Join-Path $fixture $relative)).Hash -eq $hashes[$relative]) "Preserved hash: $relative" }
Assert-Check (Test-Path -LiteralPath (Join-Path $fixture 'runtime\compact-boot.enabled')) 'Cleanup enables compact boot in preview'
$referenced = Make-Fixture 'referenced-image'
Set-Content -LiteralPath (Join-Path $referenced 'runtime\avd\APKRunner.avd\config.ini') -Value 'image.sysdir.1 = system-images/android-35/default/x86_64/'
$refused = $false
try { & $clean -AppRoot $referenced -Apply | Out-Null } catch { $refused = $_.Exception.Message -like '*referenced by an active AVD*' }
Assert-Check $refused 'Referenced system image blocks cleanup'
$linked = Make-Fixture 'junction'
$outside = Join-Path $testRoot 'outside-runtime'
New-Item -ItemType Directory -Path $outside | Out-Null
Set-Content -LiteralPath (Join-Path $outside 'preserve.txt') -Value 'Outside target'
$junction = Join-Path $linked 'runtime\avd\APKRunner.avd\snapshots\external'
New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
$refused = $false
try { & $clean -AppRoot $linked -Apply | Out-Null } catch { $refused = $_.Exception.Message -like '*Link/junction refused*' }
Assert-Check $refused 'Junction blocks cleanup before deletion'
Assert-Check (Test-Path -LiteralPath (Join-Path $outside 'preserve.txt')) 'Outside file preserved'
# Remove only the exact test-created junction, without recursive traversal.
$resolvedFixture = [IO.Path]::GetFullPath($testRoot).TrimEnd('\') + '\'
$resolvedJunction = [IO.Path]::GetFullPath($junction)
if (!$resolvedJunction.StartsWith($resolvedFixture, [StringComparison]::OrdinalIgnoreCase) -or
    !((Get-Item -LiteralPath $junction -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Refusing to remove a path that is not the fixture-created junction.'
}
# Nonrecursive deletion removes the junction itself, never its target.
[IO.Directory]::Delete($resolvedJunction, $false)
Write-Output "Test evidence: $testRoot"
