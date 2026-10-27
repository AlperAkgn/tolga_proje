param([ValidateRange(1024, 65535)][int]$Port = 5019)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskProject = Join-Path $taskRoot 'backend\Afterform.Api\Afterform.Api.csproj'
$taskRunnerRoot = Join-Path $taskRoot 'tests\postman'
$taskRunner = Join-Path $taskRunnerRoot 'run-security.cjs'
if ($Port -eq 5000) { throw 'Gunluk sunucudan ayri bir test portu kullan. Varsayilan: 5019.' }
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot '.local\backend.settings.json'))) {
    throw 'Once mevcut Docker MySQL baglantisini hazirla.'
}
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.local\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $taskRoot
try {
    Write-Host '9. adim: iki ayri QA hesabi, yapay WAV, Postman testleri. Mevcut artist parolasi istenmez.'
    Write-Host 'Basarili QA kayitlari Workbench incelemesi icin tutulur; QA B kilitlenme denemesinde 15 dakika kilitlenir.'
    if (-not (Test-Path -LiteralPath (Join-Path $taskRunnerRoot 'node_modules\newman\index.js'))) {
        Write-Host 'Yerel Newman bagimliliklari kuruluyor.'
        & npm.cmd ci --prefix $taskRunnerRoot --cache (Join-Path $taskRoot '.local\npm-cache') --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Newman kurulumu basarisiz oldu.' }
    }
    Write-Host 'Backend ayri SecurityChecks yapilandirmasinda derleniyor.'
    & dotnet build $taskProject --no-restore --configuration SecurityChecks /p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) { throw 'Test backend derlenemedi.' }
    & node $taskRunner $Port
    if ($LASTEXITCODE -ne 0) { throw 'Kontroller tamamlanamadi veya basarisiz senaryo var. docs\09-last-run.md raporunu incele.' }
    Write-Host 'Tamamlandi. Sonuc: docs\09-last-run.md'
} finally { Pop-Location }
