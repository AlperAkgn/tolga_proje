param(
    [ValidatePattern('^[a-zA-Z0-9._@+\-]{1,64}$')]
    [string]$UserName = 'mira.sol',
    [ValidateLength(1, 120)]
    [string]$DisplayName = 'Mira Sol',
    [ValidateRange(1024, 65535)]
    [int]$Port = 5000
)
$ErrorActionPreference = 'Stop'
Write-Host '[1/2] Identity ile ilk sanatci hesabi olusturuluyor.'
& (Join-Path $PSScriptRoot 'Create-Artist.ps1') -UserName $UserName -DisplayName $DisplayName
Write-Host "[2/2] Guncel sanatci giris sunucusu baslatiliyor: http://localhost:$Port/artist/login/"
Write-Host "Kullanici adi: $UserName. Parola, az once belirledigin parola. Bu pencere acik kalmali."
& (Join-Path $PSScriptRoot 'Start-Backend.ps1') -Port $Port
