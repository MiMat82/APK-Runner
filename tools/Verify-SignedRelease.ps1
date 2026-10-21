param([Parameter(Mandatory=$true)][string]$ReleaseDirectory, [Parameter(Mandatory=$true)][string]$Version,
    [string]$ConfigurationPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'release\signing.local.json'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Signing.ps1')
if (!(Test-Path -LiteralPath $ConfigurationPath)) { throw 'Configure the real validated publisher before verifying or publishing a signed release.' }
$configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
if ($configuration.Trust -ne 'PublicTrust' -or !$configuration.ExpectedSubject -or $configuration.ExpectedSubject -match 'REPLACE|<|>') { throw 'A concrete public-trust publisher subject is required.' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version.' }
$project = Split-Path $PSScriptRoot -Parent
$inspection = Join-Path $project ('analysis\signature-inspection-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $inspection | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($package in @(
    @{Name=('APKRunner-'+$Version+'-full.nupkg');Folder='package'},
    @{Name='APKRunner-win-Portable.zip';Folder='portable'})) {
    $archivePath = Join-Path $ReleaseDirectory $package.Name
    $destination = Join-Path $inspection $package.Folder
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $archive.Entries) {
            $candidate = [IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
            if (!$candidate.StartsWith($destination.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escaped inspection directory.' }
        }
    } finally { $archive.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $destination)
}
$binaries = @(Get-ChildItem -LiteralPath $inspection -Recurse -File | Where-Object {$_.Extension -in @('.exe','.dll')})
$binaries += Get-Item -LiteralPath (Join-Path $ReleaseDirectory 'APKRunner-win-Setup.exe')
$records = @(foreach ($binary in $binaries) {
    $signature = Get-AuthenticodeSignature -LiteralPath $binary.FullName
    $ours = $binary.Name -in @('APKRunner.exe','APK Runner.exe','Update.exe','APKRunner-win-Setup.exe')
    Test-ApkSignatureRecord $signature $configuration.ExpectedSubject -RequirePublisher:$ours | Out-Null
    $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($signature.SignerCertificate)
    if (!$rsa -or $rsa.KeySize -lt 2048) { throw 'A shipped binary lacks a supported RSA code-signing certificate.' }
    $rsa.Dispose()
    [pscustomobject]@{file=$binary.FullName;sha256=(Get-FileHash -LiteralPath $binary.FullName -Algorithm SHA256).Hash;
        publisher=$signature.SignerCertificate.Subject;thumbprint=$signature.SignerCertificate.Thumbprint;
        timestamp_publisher=$signature.TimeStamperCertificate.Subject;status=[string]$signature.Status;ours=$ours}
})
foreach ($required in @('APKRunner.exe','APK Runner.exe','Update.exe','APKRunner-win-Setup.exe')) {
    if (!@($binaries | Where-Object {$_.Name -eq $required}).Count) { throw ('Missing required signed binary: '+$required) }
}
$report = Join-Path $inspection 'verified-signatures.json'
[ordered]@{version=$Version;publisher=$configuration.ExpectedSubject;binaries=$records;verified_utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $report -Encoding UTF8
Write-Output ('Verified trusted publisher and timestamps on '+$records.Count+' packaged binaries. Report: '+$report)
