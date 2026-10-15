# Creeaza un certificat HTTPS pentru rularea pe doua calculatoare din aceeasi retea.
# Certificatul de dezvoltare .NET este valabil doar pentru "localhost"; acesta este valabil si pentru IP-ul dat.
# Scrie doar fisiere in certs/ (nu modifica magazinul de certificate):
#   certs/<nume>.pfx  - folosit de Broker sau Receiver pe acest calculator (contine cheia privata, nu il trimite nimanui)
#   certs/<nume>.cer  - partea publica, de trimis celuilalt calculator pentru a fi marcata ca de incredere
#
# Exemplu: powershell -ExecutionPolicy Bypass -File scripts\New-LanCertificate.ps1 -IpAddress 172.20.10.3 -Name broker -Password $env:CERT_PASSWORD
param(
     [Parameter(Mandatory = $true)][string]$IpAddress,
     [Parameter(Mandatory = $true)][string]$Name,
     # Parola fisierului .pfx; o alegi tu si nu se pastreaza in repository.
     [Parameter(Mandatory = $true)][string]$Password
)

# Oricare eroare opreste scriptul, ca sa nu ramana un certificat incomplet.
$ErrorActionPreference = "Stop"

$ip = $null
if (-not [System.Net.IPAddress]::TryParse($IpAddress, [ref]$ip)) {
     throw "'$IpAddress' nu este o adresa IP. Foloseste IP-ul din ipconfig, de exemplu 172.20.10.4."
}
$key = [System.Security.Cryptography.RSA]::Create(2048)
$request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
     "CN=$IpAddress", $key, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
     [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

$san = New-Object System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
$san.AddIpAddress($ip)
$san.AddDnsName("localhost")
$request.CertificateExtensions.Add($san.Build())

$usages = New-Object System.Security.Cryptography.OidCollection
$usages.Add((New-Object System.Security.Cryptography.Oid "1.3.6.1.5.5.7.3.1")) | Out-Null  # server authentication
$request.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension $usages, $false))

$certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(1))

$folder = Join-Path (Split-Path $PSScriptRoot -Parent) "certs"
New-Item -ItemType Directory -Force $folder | Out-Null
$pfx = Join-Path $folder "$Name.pfx"
$cer = Join-Path $folder "$Name.cer"
[System.IO.File]::WriteAllBytes($pfx, $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password))
[System.IO.File]::WriteAllBytes($cer, $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))

Write-Host "Certificat pentru $IpAddress si localhost:"
Write-Host "  $pfx  (ramane pe acest calculator)"
Write-Host "  $cer  (trimite-l celuilalt calculator)"
