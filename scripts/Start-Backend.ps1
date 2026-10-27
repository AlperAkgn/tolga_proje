param([ValidateRange(1024, 65535)][int]$Port = 5000)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskProject = Join-Path $taskRoot 'backend\Afterform.Api\Afterform.Api.csproj'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.local\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $taskRoot
try {
    dotnet run --project $taskProject --no-restore --launch-profile Afterform.Api --urls "http://localhost:$Port"
    if ($LASTEXITCODE -ne 0) { throw 'The backend did not start successfully.' }
} finally { Pop-Location }
