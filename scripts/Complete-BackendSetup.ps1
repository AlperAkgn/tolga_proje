param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5000
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSettingsPath = Join-Path $taskRoot '.local\backend.settings.json'
if (-not (Test-Path -LiteralPath $taskSettingsPath)) {
    throw 'Once Initialize-Database.ps1 ile yerel veritabani baglantisini hazirla.'
}

Write-Host '[1/2] EF Core migration mevcut MySQL veritabanina uygulanacak.'
& (Join-Path $PSScriptRoot 'Update-Database.ps1')
Write-Host "[2/2] AFTER/FORM sunucusu baslatiliyor: http://localhost:$Port/"
Write-Host 'Bu pencere sunucu calisirken acik kalmali. Durdurmak icin Ctrl+C.'
& (Join-Path $PSScriptRoot 'Start-Backend.ps1') -Port $Port
