$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskProject = Join-Path $taskRoot 'backend\Afterform.Api'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.local\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if (-not (Test-Path (Join-Path $taskRoot '.local\backend.settings.json')) -and
    -not $env:ConnectionStrings__AfterformMigrations) {
    throw 'First configure the database with Initialize-Database.ps1 or environment variables.'
}
Push-Location $taskRoot
try {
    dotnet tool restore --configfile (Join-Path $taskRoot 'NuGet.config')
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    dotnet build $taskProject --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
    dotnet ef database update --project $taskProject --startup-project $taskProject --no-build
    if ($LASTEXITCODE -ne 0) { throw 'The migration did not complete.' }
} finally { Pop-Location }
