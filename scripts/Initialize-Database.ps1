param(
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]*$')]
    [string]$ContainerName,
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]{0,63}$')]
    [string]$DatabaseName = 'afterform'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskLocal = Join-Path $taskRoot '.local'
New-Item -ItemType Directory -Force -Path $taskLocal | Out-Null
Write-Host '[1/5] Docker motoru denetleniyor. Her kontrol en fazla 10 saniye bekler.'

$taskDockerExe = (Get-Command docker -ErrorAction Stop).Source
$taskDockerArgs = @('--config', (Join-Path $taskLocal 'docker-cli'))

# Quote native Windows arguments without using another shell. Works in PS 5.1 and 7.
function ConvertTo-TaskArgument([string]$Argument) {
    $taskEscaped = [regex]::Replace($Argument, '(\\*)"', '$1$1\"')
    $taskEscaped = [regex]::Replace($taskEscaped, '(\\+)$', '$1$1')
    return '"' + $taskEscaped + '"'
}
function Invoke-TaskDocker {
    param([string[]]$Arguments, [string]$InputText, [int]$TimeoutSeconds = 15)
    $taskInfo = New-Object System.Diagnostics.ProcessStartInfo
    $taskInfo.FileName = $taskDockerExe
    $taskInfo.Arguments = (($taskDockerArgs + $Arguments) |
        ForEach-Object { ConvertTo-TaskArgument $_ }) -join ' '
    $taskInfo.UseShellExecute = $false
    $taskInfo.CreateNoWindow = $true
    $taskInfo.RedirectStandardOutput = $true
    $taskInfo.RedirectStandardError = $true
    $taskInfo.RedirectStandardInput = $true
    $taskProcess = [System.Diagnostics.Process]::Start($taskInfo)
    $taskOutputTask = $taskProcess.StandardOutput.ReadToEndAsync()
    $taskErrorTask = $taskProcess.StandardError.ReadToEndAsync()
    if ($InputText) { $taskProcess.StandardInput.WriteLine($InputText) }
    $taskProcess.StandardInput.Close()
    if (-not $taskProcess.WaitForExit($TimeoutSeconds * 1000)) {
        $taskProcess.Kill()
        $taskProcess.Dispose()
        throw "Docker komutu $TimeoutSeconds saniyede yanit vermedi. Docker Desktop motorunun hazir oldugunu kontrol et."
    }
    $taskResult = [PSCustomObject]@{
        ExitCode = $taskProcess.ExitCode
        Output = $taskOutputTask.GetAwaiter().GetResult().Trim()
        ErrorText = $taskErrorTask.GetAwaiter().GetResult().Trim()
    }
    $taskProcess.Dispose()
    return $taskResult
}
function Assert-TaskDocker($Result, [string]$Message) {
    if ($Result.ExitCode -ne 0) { throw "$Message $($Result.ErrorText)" }
}

$taskHosts = @('npipe:////./pipe/dockerDesktopLinuxEngine', 'npipe:////./pipe/docker_engine')
$taskConnected = $false
foreach ($taskHost in $taskHosts) {
    $taskResult = Invoke-TaskDocker -Arguments @('--host', $taskHost, 'info', '--format', '{{.OSType}}') -TimeoutSeconds 10
    if ($taskResult.ExitCode -eq 0) {
        $taskDockerArgs += @('--host', $taskHost)
        $taskConnected = $true
        break
    }
}
if (-not $taskConnected) {
    throw "Docker motoruna baglanilamadi. Docker Desktop'i acip Engine running durumunu bekle. $($taskResult.ErrorText)"
}

Write-Host '[2/5] Mevcut MySQL konteyneri araniyor.'
if (-not $ContainerName) {
    $taskResult = Invoke-TaskDocker -Arguments @('ps', '-a', '--format', '{{json .}}')
    Assert-TaskDocker $taskResult 'Konteyner listesi okunamadi.'
    $taskContainers = @($taskResult.Output -split '\r?\n' |
        Where-Object { $_ } |
        ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object { $_.Image -match '(^|/)(mysql|mysql-server)(:|@|$)' })
    if ($taskContainers.Count -ne 1) {
        $taskCandidates = ($taskContainers | ForEach-Object { $_.Names }) -join ', '
        throw "Tek bir MySQL konteyneri secilemedi ($taskCandidates). -ContainerName ile mevcut konteyner adini ver."
    }
    $ContainerName = $taskContainers[0].Names
}
$taskResult = Invoke-TaskDocker -Arguments @('inspect', '--format', '{{.State.Running}}', $ContainerName)
Assert-TaskDocker $taskResult 'MySQL konteyneri bulunamadi.'
if ($taskResult.Output -ne 'true') {
    Write-Host "MySQL konteyneri baslatiliyor: $ContainerName"
    $taskResult = Invoke-TaskDocker -Arguments @('start', $ContainerName) -TimeoutSeconds 30
    Assert-TaskDocker $taskResult 'MySQL konteyneri baslatilamadi.'
}
$taskResult = Invoke-TaskDocker -Arguments @('port', $ContainerName, '3306/tcp')
Assert-TaskDocker $taskResult 'MySQL port eslemesi okunamadi.'
$taskPortLine = $taskResult.Output -split '\r?\n' |
    Where-Object { $_ -match ':(\d+)$' } | Select-Object -First 1
if (-not $taskPortLine) { throw 'MySQL konteynerinin 3306 portu bilgisayara yayimlanmis olmali.' }
$taskPort = [int]([regex]::Match($taskPortLine, ':(\d+)$').Groups[1].Value)

# The root credential stays in its container and never appears in console output.
$taskMysqlShell = 'if [ -n "$MYSQL_ROOT_PASSWORD_FILE" ]; then MYSQL_PWD="$(cat "$MYSQL_ROOT_PASSWORD_FILE")"; else MYSQL_PWD="$MYSQL_ROOT_PASSWORD"; fi; export MYSQL_PWD; exec mysql --user=root --batch --skip-column-names'
function Invoke-TaskSql([string]$Sql) {
    $taskResult = Invoke-TaskDocker -Arguments @('exec', '-i', $ContainerName, 'sh', '-c', $taskMysqlShell) -InputText $Sql -TimeoutSeconds 15
    if ($taskResult.ExitCode -ne 0) {
        # SQL errors can quote statement text; never print credential-bearing SQL.
        throw 'MySQL komutu basarisiz. MySQL hazirligini ve konteynerdeki root parola ayarini kontrol et.'
    }
    return $taskResult.Output
}

Write-Host "[3/5] MySQL yaniti bekleniyor: $ContainerName (host portu $taskPort)."
$taskVersion = $null
for ($taskAttempt = 1; $taskAttempt -le 5; $taskAttempt++) {
    try { $taskVersion = Invoke-TaskSql 'SELECT VERSION();'; break }
    catch {
        if ($taskAttempt -eq 5) { throw }
        Write-Host "MySQL henuz yanit vermedi; yeniden kontrol ediliyor ($taskAttempt/5)."
        Start-Sleep -Seconds 2
    }
}
if ([int]($taskVersion.Split('.')[0]) -lt 8) {
    throw "Bu sema MySQL 8.0 veya sonrasi icin hazirlandi; bulunan surum: $taskVersion."
}

$taskSettingsPath = Join-Path $taskLocal 'backend.settings.json'
if (Test-Path -LiteralPath $taskSettingsPath) {
    $taskSaved = Get-Content -LiteralPath $taskSettingsPath -Raw | ConvertFrom-Json
    if ($taskSaved.Setup.ContainerName -ne $ContainerName -or $taskSaved.Setup.DatabaseName -ne $DatabaseName) {
        throw 'Yerel ayar dosyasi baska bir veritabanina ait; mevcut duzeni degistirmeden o ayarlari kullan.'
    }
    Write-Host "[5/5] Yerel baglanti zaten hazir: $DatabaseName / $ContainerName."
    return
}

Write-Host "[4/5] $DatabaseName icin proje kullanicilari hazirlaniyor."
$taskExistingTables = Invoke-TaskSql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$DatabaseName';"
if ([int]$taskExistingTables -gt 0) {
    throw "$DatabaseName veritabaninda zaten tablolar var. -DatabaseName ile bu projeye ozel bos bir veritabani sec."
}
function New-TaskPassword {
    $taskBytes = New-Object byte[] 24
    $taskGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $taskGenerator.GetBytes($taskBytes) } finally { $taskGenerator.Dispose() }
    return [BitConverter]::ToString($taskBytes).Replace('-', '')
}
function Get-TaskNewUserName([string]$BaseName) {
    $taskExists = Invoke-TaskSql "SELECT COUNT(*) FROM mysql.user WHERE User='$BaseName';"
    if ([int]$taskExists -eq 0) { return $BaseName }
    return $BaseName + '_' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
}
$taskAppUser = Get-TaskNewUserName 'afterform_app'
$taskMigrationUser = Get-TaskNewUserName 'afterform_migrator'
$taskAppPassword = New-TaskPassword
$taskMigrationPassword = New-TaskPassword
$taskQuotedDatabase = [string][char]96 + $DatabaseName + [char]96

$taskSql = @"
CREATE DATABASE IF NOT EXISTS $taskQuotedDatabase CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER '$taskAppUser'@'%' IDENTIFIED BY '$taskAppPassword';
GRANT SELECT, INSERT, UPDATE, DELETE ON $taskQuotedDatabase.* TO '$taskAppUser'@'%';
CREATE USER '$taskMigrationUser'@'%' IDENTIFIED BY '$taskMigrationPassword';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES ON $taskQuotedDatabase.* TO '$taskMigrationUser'@'%';
"@
Invoke-TaskSql $taskSql | Out-Null

$taskSettings = @{
    ConnectionStrings = @{
        Afterform = "Server=127.0.0.1;Port=$taskPort;Database=$DatabaseName;User ID=$taskAppUser;Password=$taskAppPassword;SslMode=Preferred;"
        AfterformMigrations = "Server=127.0.0.1;Port=$taskPort;Database=$DatabaseName;User ID=$taskMigrationUser;Password=$taskMigrationPassword;SslMode=Preferred;"
    }
    Setup = @{ ContainerName = $ContainerName; DatabaseName = $DatabaseName; HostPort = $taskPort }
}
$taskSettings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $taskSettingsPath -Encoding utf8
Write-Host "[5/5] Tamamlandi: $DatabaseName / 127.0.0.1:$taskPort / MySQL $taskVersion."
Write-Host "Uygulama kullanicisi: $taskAppUser. Sema kullanicisi: $taskMigrationUser."
Write-Host 'Parolalar ekrana yazilmadan .local klasorune kaydedildi.'
Write-Host 'Sonraki islem: EF migration uygulanmasi.'
