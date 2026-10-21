function Get-ApkSigningArguments {
    param([string]$ConfigurationPath)
    if (!(Test-Path -LiteralPath $ConfigurationPath -PathType Leaf)) { throw 'Configure a validated publisher in release\signing.local.json before making a signed release.' }
    $configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
    if (!$configuration.ExpectedSubject -or $configuration.ExpectedSubject -match '<|>|REPLACE') { throw 'ExpectedSubject must match the certificate issued to the validated publisher.' }
    if ($configuration.Trust -ne 'PublicTrust') { throw 'Public distribution requires PublicTrust; private or test identities are not accepted.' }
    if ($configuration.Provider -eq 'AzureArtifactSigning') {
        if ($configuration.Endpoint -notmatch '^https://[a-z0-9-]+\.codesigning\.azure\.net/?$') { throw 'Use an official HTTPS Azure Artifact Signing endpoint.' }
        foreach ($value in @($configuration.CodeSigningAccountName,$configuration.CertificateProfileName)) {
            if (!$value -or $value -notmatch '^[A-Za-z0-9-]{3,64}$') { throw 'Specify the actual signing account and public-trust certificate profile names.' }
        }
        $metadata = [ordered]@{Endpoint=$configuration.Endpoint;CodeSigningAccountName=$configuration.CodeSigningAccountName;CertificateProfileName=$configuration.CertificateProfileName}
        $project = Split-Path $PSScriptRoot -Parent
        $metadataDirectory = Join-Path $project 'analysis'
        New-Item -ItemType Directory -Path $metadataDirectory -Force | Out-Null
        $metadataPath = Join-Path $metadataDirectory ('signing-metadata-' + [guid]::NewGuid().ToString('N') + '.json')
        $metadata | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding UTF8
        return [pscustomobject]@{Provider=$configuration.Provider;ExpectedSubject=$configuration.ExpectedSubject;Arguments=@('--azureTrustedSignFile',$metadataPath)}
    }
    if ($configuration.Provider -ne 'CertificateStore') { throw 'Signing provider must be CertificateStore or AzureArtifactSigning.' }
    $thumbprint = ([string]$configuration.Thumbprint).Replace(' ','').ToUpperInvariant()
    if ($thumbprint -notmatch '^[A-F0-9]{40}$' -or $configuration.StoreLocation -notin @('CurrentUser','LocalMachine')) { throw 'Choose a concrete certificate thumbprint in CurrentUser or LocalMachine My.' }
    $certificatePath = 'Cert:\' + $configuration.StoreLocation + '\My\' + $thumbprint
    if (!(Test-Path -LiteralPath $certificatePath)) { throw 'The selected publisher code-signing certificate is not installed.' }
    $certificate = Get-Item -LiteralPath $certificatePath
    if (!$certificate.HasPrivateKey -or $certificate.NotBefore -gt (Get-Date) -or $certificate.NotAfter -le (Get-Date)) { throw 'The code-signing identity needs an available private key and valid certificate dates.' }
    if ($certificate.Subject -cne $configuration.ExpectedSubject -or $certificate.Subject -eq $certificate.Issuer) { throw 'The certificate must belong to the exact configured publisher and cannot be self-signed.' }
    $usage = $certificate.Extensions | Where-Object {$_.Oid.Value -eq '2.5.29.37'} | Select-Object -First 1
    if (!$usage -or @($usage.EnhancedKeyUsages | Where-Object {$_.Value -eq '1.3.6.1.5.5.7.3.3'}).Count -eq 0) { throw 'The selected certificate is not a code-signing certificate.' }
    $signParameters = '/sha1 ' + $thumbprint + ' /s My /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com'
    if ($configuration.StoreLocation -eq 'LocalMachine') { $signParameters += ' /sm' }
    return [pscustomobject]@{Provider=$configuration.Provider;ExpectedSubject=$configuration.ExpectedSubject;Arguments=@('--signParams',$signParameters)}
}
function Test-ApkSignatureRecord {
    param($Signature, [string]$ExpectedSubject, [switch]$RequirePublisher)
    if ([string]$Signature.Status -ne 'Valid' -or !$Signature.SignerCertificate) { throw 'A shipped binary does not have a valid Windows-trusted Authenticode signature.' }
    if (!$Signature.TimeStamperCertificate) { throw 'A shipped binary lacks a timestamp; do not publish it.' }
    if ($RequirePublisher -and $Signature.SignerCertificate.Subject -cne $ExpectedSubject) { throw 'The publisher on a shipped application or updater does not match the configured identity.' }
    return $true
}
