param([ValidateRange(1024, 65535)][int]$Port = 8080)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSource = Join-Path $taskRoot '.local\backend.settings.json'
$taskDirectory = Join-Path $taskRoot '.local\docker'
if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf)) {
    throw 'Once mevcut MySQL icin .local/backend.settings.json hazirlanmis olmali.'
}

try {
    $taskSaved = Get-Content -LiteralPath $taskSource -Raw | ConvertFrom-Json
    $taskConnection = New-Object System.Data.Common.DbConnectionStringBuilder
    # Explicit accessors avoid PowerShell treating ConnectionString as a dictionary key.
    $taskConnection.set_ConnectionString([string]$taskSaved.ConnectionStrings.Afterform)
    if ([string]::IsNullOrWhiteSpace([string]$taskConnection['Password']) -or
        [string]$taskConnection['User ID'] -notmatch '^afterform_app(?:_[a-zA-Z0-9]+)?$' -or
        [string]$taskConnection['Database'] -ne [string]$taskSaved.Setup.DatabaseName) {
        throw 'Invalid runtime configuration.'
    }
    # Use the existing application user. Never mount root/migrator credentials.
    $taskConnection['Server'] = 'afterform-mysql'
    $taskConnection['Port'] = 3306
    $taskRuntime = @{ ConnectionStrings = @{ Afterform = $taskConnection.get_ConnectionString() } }
} catch {
    throw 'Yerel uygulama baglantisi okunamadi. Ayar dosyasini veya parolayi paylasma.'
}

New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $taskDirectory 'data-protection') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $taskRoot '.local\uploads\demos') | Out-Null
$taskEncoding = New-Object System.Text.UTF8Encoding($false)
$taskSecretPath = Join-Path $taskDirectory 'backend.settings.json'
$taskTemporaryPath = Join-Path $taskDirectory ('runtime-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    [IO.File]::WriteAllText($taskTemporaryPath, ($taskRuntime | ConvertTo-Json -Depth 4), $taskEncoding)
    Move-Item -LiteralPath $taskTemporaryPath -Destination $taskSecretPath -Force
} finally {
    if (Test-Path -LiteralPath $taskTemporaryPath) { Remove-Item -LiteralPath $taskTemporaryPath }
    $taskRuntime = $null
    $taskConnection = $null
    $taskSaved = $null
}
[IO.File]::WriteAllText((Join-Path $taskDirectory 'compose.env'), "AF_HTTP_PORT=$Port`n", $taskEncoding)
Write-Host "Docker ayarlari hazir. Yerel port: $Port. Parola ekrana yazilmadi."
