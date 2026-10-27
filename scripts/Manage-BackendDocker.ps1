param([ValidateSet('Status', 'Logs', 'Stop', 'Restart', 'Recreate')][string]$Action = 'Status')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Docker-Common.ps1')
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot '.local\docker\compose.env'))) {
    throw 'Once Start-BackendDocker.ps1 ile kurulum yap.'
}
Assert-AfterformProjectOwnership
$taskPortFile = Join-Path $taskRoot '.local\docker\compose.env'
$taskPortLine = Get-Content -LiteralPath $taskPortFile | Where-Object { $_ -match '^AF_HTTP_PORT=\d+$' } | Select-Object -First 1
if (-not $taskPortLine) { throw 'Kayitli Docker portu okunamadi; Start-BackendDocker.ps1 ile ayarlari yenile.' }
$taskOldPort = [Environment]::GetEnvironmentVariable('AF_HTTP_PORT', 'Process')
switch ($Action) {
    'Status' { $taskActionArgs = @('ps', '--all') }
    'Logs' { $taskActionArgs = @('logs', '--tail', '100', 'backend') }
    'Stop' { $taskActionArgs = @('stop', 'backend') }
    'Restart' { $taskActionArgs = @('restart', 'backend') }
    'Recreate' { $taskActionArgs = @('up', '--detach', '--no-build', '--force-recreate', '--wait', '--wait-timeout', '120', 'backend') }
}
try {
    [Environment]::SetEnvironmentVariable('AF_HTTP_PORT', $taskPortLine.Split('=')[1], 'Process')
    & $taskDockerExe @taskDockerArgs @taskComposeArgs @taskActionArgs
    if ($LASTEXITCODE -ne 0) { throw "Docker $Action islemi tamamlanamadi." }
} finally { [Environment]::SetEnvironmentVariable('AF_HTTP_PORT', $taskOldPort, 'Process') }
