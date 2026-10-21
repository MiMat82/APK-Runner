$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
. (Join-Path $project 'tools\Signing.ps1')
$fixture = Join-Path $project ('analysis\signing-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$script:checks = 0
function Check($condition, [string]$message) { if (!$condition) { throw $message }; $script:checks++; "PASS $message" }
function Refuses([scriptblock]$action) { try { & $action | Out-Null; return $false } catch { return $true } }
Check (Refuses { Get-ApkSigningArguments -ConfigurationPath (Join-Path $fixture 'missing.json') }) 'Missing signing configuration fails closed'
$config = Join-Path $fixture 'signing.json'
@{Provider='AzureArtifactSigning';Trust='PublicTrust';ExpectedSubject='CN=Example Publisher';Endpoint='https://weu.codesigning.azure.net';CodeSigningAccountName='example-account';CertificateProfileName='production'} | ConvertTo-Json | Set-Content -LiteralPath $config
$arguments = Get-ApkSigningArguments -ConfigurationPath $config
Check ($arguments.Provider -eq 'AzureArtifactSigning' -and $arguments.ExpectedSubject -eq 'CN=Example Publisher') 'Public trust signing metadata retains the explicitly configured publisher'
Check ($arguments.Arguments[0] -eq '--azureTrustedSignFile') 'Azure signing is passed to the packager before packages are created'
$metadata = Get-Content -LiteralPath $arguments.Arguments[1] -Raw | ConvertFrom-Json
Check ($metadata.Endpoint -eq 'https://weu.codesigning.azure.net' -and !$metadata.AccessToken) 'Generated cloud signing metadata contains no access token'
@{Provider='AzureArtifactSigning';Trust='PublicTrust';ExpectedSubject='CN=Example Publisher';Endpoint='https://example.invalid';CodeSigningAccountName='account';CertificateProfileName='profile'} | ConvertTo-Json | Set-Content -LiteralPath $config
Check (Refuses { Get-ApkSigningArguments -ConfigurationPath $config }) 'Cloud credentials cannot be sent to an arbitrary signing endpoint'
@{Provider='CertificateStore';Trust='PublicTrust';ExpectedSubject='CN=Example Publisher';Thumbprint=('0'*40);StoreLocation='CurrentUser'} | ConvertTo-Json | Set-Content -LiteralPath $config
Check (Refuses { Get-ApkSigningArguments -ConfigurationPath $config }) 'A missing signing identity cannot produce an unsigned release'
$unsigned = [pscustomobject]@{Status='NotSigned';SignerCertificate=$null;TimeStamperCertificate=$null}
Check (Refuses { Test-ApkSignatureRecord $unsigned 'CN=Example Publisher' -RequirePublisher }) 'Unsigned application binaries are rejected'
$valid = [pscustomobject]@{Status='Valid';SignerCertificate=[pscustomobject]@{Subject='CN=Example Publisher'};TimeStamperCertificate=[pscustomobject]@{Subject='CN=Timestamp CA'}}
Check (Test-ApkSignatureRecord $valid 'CN=Example Publisher' -RequirePublisher) 'Valid timestamped expected publisher is accepted by the policy'
Check (Refuses { Test-ApkSignatureRecord $valid 'CN=Other Publisher' -RequirePublisher }) 'A different valid publisher does not satisfy our publisher requirement'
$valid.TimeStamperCertificate=$null
Check (Refuses { Test-ApkSignatureRecord $valid 'CN=Example Publisher' -RequirePublisher }) 'Missing timestamp cannot pass the release gate'
$tampered=[pscustomobject]@{Status='HashMismatch';SignerCertificate=[pscustomobject]@{Subject='CN=Example Publisher'};TimeStamperCertificate=[pscustomobject]@{Subject='CN=Timestamp CA'}}
Check (Refuses { Test-ApkSignatureRecord $tampered 'CN=Example Publisher' -RequirePublisher }) 'Tampering is rejected even when signer information is present'
"$script:checks signing checks passed."
