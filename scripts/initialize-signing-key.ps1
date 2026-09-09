param([Parameter(Mandatory)][string]$PrivateKeyPath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$keyFile = [IO.Path]::GetFullPath($PrivateKeyPath)

if ($keyFile.StartsWith(
        $root + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase))
{
    throw 'Private keys must be stored outside the repository.'
}

if (Test-Path -LiteralPath $keyFile)
{
    throw 'Refusing to overwrite an existing private key.'
}

$publicFile = Join-Path $root 'apps\windows\Features\Updates\update-signing-public.pem'

if (Test-Path -LiteralPath $publicFile)
{
    throw 'A public key is already configured. Key rotation requires a separate migration.'
}

$password = Read-Host 'Passphrase for the new WeekNumber signing key' -AsSecureString
$credential = [Net.NetworkCredential]::new('', $password)

if ([string]::IsNullOrWhiteSpace($credential.Password))
{
    throw 'A nonempty passphrase is required.'
}

$rsa = [Security.Cryptography.RSA]::Create(3072)

try
{
    $parameters = [Security.Cryptography.PbeParameters]::new(
        [Security.Cryptography.PbeEncryptionAlgorithm]::Aes256Cbc,
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        210000)
    $pem = $rsa.ExportEncryptedPkcs8PrivateKeyPem($credential.Password, $parameters)

    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($keyFile)) -Force | Out-Null
    [IO.File]::WriteAllText($keyFile, $pem)
    [IO.File]::WriteAllText($publicFile, $rsa.ExportSubjectPublicKeyInfoPem())
    Write-Output 'Dedicated key created. Rebuild all release artifacts before signing.'
}
finally
{
    $rsa.Dispose()
    $password.Dispose()

    $credential = $null
    $pem = $null
}
