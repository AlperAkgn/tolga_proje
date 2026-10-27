param(
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]*$')][string]$ContainerName,
    [ValidateRange(1024, 65535)][int]$Port = 8080
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Docker-Common.ps1')
$taskStage = 1
$taskReceipt = [ordered]@{
    Status = 'preparing'; StartedAtUtc = [DateTime]::UtcNow.ToString('o')
    Address = "http://localhost:$Port"; MySqlContainerId = $null
    MySqlDataMount = $null; Network = 'afterform-local'
    DemoDirectory = (Join-Path $taskRoot '.local\uploads\demos')
    KeyDirectory = (Join-Path $taskRoot '.local\docker\data-protection')
    BackendContainerId = $null; Database = $null; Schema = $null
}
$taskOldPort = [Environment]::GetEnvironmentVariable('AF_HTTP_PORT', 'Process')
try {
    Write-Host '[1/6] Docker motoru ve Linux konteyner destegi denetleniyor.'
    $taskResult = Invoke-AfterformDocker -Arguments @('info', '--format', '{{.OSType}}') -TimeoutSeconds 10
    Assert-AfterformDocker $taskResult 'Docker motoruna erisilemiyor. Docker Desktop acik olmali; normal PowerShell kullan.'
    if ($taskResult.Output -ne 'linux') { throw 'Docker Desktop Linux containers modunda olmali.' }
    $taskResult = Invoke-AfterformDocker -Arguments @('compose', 'version', '--short')
    Assert-AfterformDocker $taskResult 'Docker Compose v2 bulunamadi. Docker Desktop kurulumunu kontrol et.'
    Assert-AfterformProjectOwnership

    $taskStage = 2
    Write-Host '[2/6] Mevcut MySQL ve /var/lib/mysql kalici baglantisi okunuyor.'
    $taskSaved = Get-Content -LiteralPath (Join-Path $taskRoot '.local\backend.settings.json') -Raw | ConvertFrom-Json
    $taskSavedContainer = [string]$taskSaved.Setup.ContainerName
    if ([string]::IsNullOrWhiteSpace($taskSavedContainer)) { throw 'Yerel MySQL kurulum bilgisi bulunamadi.' }
    if (-not $ContainerName) { $ContainerName = $taskSavedContainer }
    $taskResult = Invoke-AfterformDocker -Arguments @('inspect', '--format', '{{.Id}}', $ContainerName)
    Assert-AfterformDocker $taskResult 'Verilen MySQL konteyneri bulunamadi.'
    $taskReceipt.MySqlContainerId = $taskResult.Output
    $taskResult = Invoke-AfterformDocker -Arguments @('inspect', '--format', '{{.Id}}', $taskSavedContainer)
    Assert-AfterformDocker $taskResult 'Kayitli MySQL konteyneri bulunamadi.'
    if ($taskResult.Output -ne $taskReceipt.MySqlContainerId) {
        throw 'Verilen konteyner kayitli afterform veritabaniyla ayni degil. Mevcut veritabani degistirilmedi.'
    }
    $taskResult = Invoke-AfterformDocker -Arguments @('inspect', '--format', '{{json .Mounts}}', $ContainerName)
    Assert-AfterformDocker $taskResult 'MySQL veri depolamasi okunamadi.'
    $taskMounts = @($taskResult.Output | ConvertFrom-Json)
    $taskDataMount = @($taskMounts | Where-Object { $_.Destination -eq '/var/lib/mysql' })
    if ($taskDataMount.Count -ne 1 -or $taskDataMount[0].Type -notin @('volume', 'bind') -or -not $taskDataMount[0].RW) {
        throw 'MySQL /var/lib/mysql icin yazilabilir kalici volume/bind baglantisi bulunamadi. Verileri tasimadan once birlikte incelemeliyiz.'
    }
    $taskReceipt.MySqlDataMount = @{ Type = $taskDataMount[0].Type; Name = $taskDataMount[0].Name;
        Source = $taskDataMount[0].Source; Destination = '/var/lib/mysql' }
    Write-Host ("MySQL kalici depolama: " + $taskDataMount[0].Type + ' / ' + $taskDataMount[0].Name)
    $taskResult = Invoke-AfterformDocker -Arguments @('inspect', '--format', '{{.State.Running}}', $ContainerName)
    Assert-AfterformDocker $taskResult 'MySQL calisma durumu okunamadi.'
    if ($taskResult.Output -ne 'true') {
        $taskResult = Invoke-AfterformDocker -Arguments @('start', $ContainerName) -TimeoutSeconds 30
        Assert-AfterformDocker $taskResult 'Mevcut MySQL baslatilamadi.'
    }
    $taskSaved = $null

    $taskStage = 3
    Write-Host '[3/6] afterform-local agi ve MySQL DNS adi hazirlaniyor.'
    $taskResult = Invoke-AfterformDocker -Arguments @('network', 'ls', '--filter',
        'name=^afterform-local$', '--format', '{{.Name}}')
    Assert-AfterformDocker $taskResult 'Docker aglari okunamadi.'
    if (-not $taskResult.Output) {
        $taskResult = Invoke-AfterformDocker -Arguments @('network', 'create', '--driver', 'bridge',
            '--label', 'com.afterform.project=artist-portal', 'afterform-local')
        Assert-AfterformDocker $taskResult 'Afterform agi olusturulamadi.'
    }
    $taskResult = Invoke-AfterformDocker -Arguments @('network', 'inspect', '--format',
        '{{ index .Labels "com.afterform.project" }}', 'afterform-local')
    Assert-AfterformDocker $taskResult 'Afterform agi okunamadi.'
    if ($taskResult.Output -ne 'artist-portal') { throw 'afterform-local adi baska bir aga ait; ag degistirilmedi.' }
    $taskResult = Invoke-AfterformDocker -Arguments @('inspect', '--format', '{{json .NetworkSettings.Networks}}', $ContainerName)
    Assert-AfterformDocker $taskResult 'MySQL ag uyeligi okunamadi.'
    $taskNetworks = $taskResult.Output | ConvertFrom-Json
    $taskMembership = $taskNetworks.PSObject.Properties['afterform-local']
    if ($null -eq $taskMembership) {
        $taskResult = Invoke-AfterformDocker -Arguments @('network', 'connect', '--alias', 'afterform-mysql',
            'afterform-local', $ContainerName)
        Assert-AfterformDocker $taskResult 'Mevcut MySQL afterform agina eklenemedi.'
    } elseif (@($taskMembership.Value.Aliases) -notcontains 'afterform-mysql') {
        throw 'MySQL afterform-local aginda fakat afterform-mysql takma adi yok; mevcut baglanti kesilmedi.'
    }

    $taskStage = 4
    Write-Host '[4/6] Uygulama sir dosyasi ve mevcut demo klasoru hazirlaniyor.'
    & (Join-Path $PSScriptRoot 'Initialize-DockerConfig.ps1') -Port $Port
    [Environment]::SetEnvironmentVariable('AF_HTTP_PORT', [string]$Port, 'Process')
    $taskResult = Invoke-AfterformDocker -Arguments ($taskComposeArgs + @('config', '--quiet'))
    Assert-AfterformDocker $taskResult 'Compose yapilandirmasi okunamadi.'

    $taskStage = 5
    Write-Host '[5/6] Docker imaji derleniyor ve backend baslatiliyor. Ilk indirme birkac dakika surebilir.'
    # Build progress is visible in the user's terminal. No existing host process is stopped.
    & $taskDockerExe @taskDockerArgs @taskComposeArgs up --build --detach --force-recreate --wait --wait-timeout 120 backend
    if ($LASTEXITCODE -ne 0) { throw 'Backend healthy duruma gelemedi. Manage-BackendDocker.ps1 -Action Logs ile incele.' }

    $taskStage = 6
    Write-Host '[6/6] Gercek MySQL baglantisi ve migration durumu okunuyor.'
    $taskReady = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/health/ready" -TimeoutSec 15
    if ($taskReady.status -ne 'ready' -or $taskReady.database -ne 'connected' -or $taskReady.schema -ne 'upToDate') {
        throw 'Backend acildi fakat MySQL/sema hazirligi dogrulanamadi.'
    }
    $taskResult = Invoke-AfterformDocker -Arguments ($taskComposeArgs + @('ps', '--quiet', 'backend'))
    Assert-AfterformDocker $taskResult 'Backend kimligi okunamadi.'
    $taskReceipt.BackendContainerId = $taskResult.Output
    $taskReceipt.Status = 'ready'
    $taskReceipt.Database = 'connected'
    $taskReceipt.Schema = 'upToDate'
    Write-Host "Tamamlandi: http://localhost:$Port/ / MySQL connected / schema upToDate."
    Write-Host 'Backend Docker arka planinda calisiyor; terminali kapatabilirsin.'
    Write-Host 'Docker oturum anahtarlari ayri oldugu icin ilk geciste tekrar giris yap.'
} catch {
    $taskReceipt.Status = 'incomplete'
    Write-Host "Docker kurulumu $taskStage. adimda durdu. Mevcut MySQL verileri silinmedi."
    throw
} finally {
    [Environment]::SetEnvironmentVariable('AF_HTTP_PORT', $taskOldPort, 'Process')
    $taskReceipt['FinishedAtUtc'] = [DateTime]::UtcNow.ToString('o')
    $taskReceipt['LastStage'] = $taskStage
    $taskReceiptDirectory = Join-Path $taskRoot '.local\docker'
    New-Item -ItemType Directory -Force -Path $taskReceiptDirectory | Out-Null
    $taskEncoding = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText((Join-Path $taskReceiptDirectory 'last-start.json'),
        ($taskReceipt | ConvertTo-Json -Depth 5), $taskEncoding)
}
