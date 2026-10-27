# Shared CLI helpers. These commands never inspect or print container environment variables.
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDockerExe = (Get-Command docker -ErrorAction Stop).Source
$taskDockerConfigDirectory = Join-Path $taskRoot '.local\docker-cli'
# A user-local Docker Desktop install keeps Compose beside resources/bin.
# A separate CLI config must explicitly retain that plugin search directory.
$taskPluginDirectory = Join-Path (Split-Path (Split-Path $taskDockerExe -Parent) -Parent) 'cli-plugins'
if (Test-Path -LiteralPath (Join-Path $taskPluginDirectory 'docker-compose.exe')) {
    New-Item -ItemType Directory -Force -Path $taskDockerConfigDirectory | Out-Null
    $taskCliConfigPath = Join-Path $taskDockerConfigDirectory 'config.json'
    if (Test-Path -LiteralPath $taskCliConfigPath) {
        $taskCliConfig = Get-Content -LiteralPath $taskCliConfigPath -Raw | ConvertFrom-Json
    } else { $taskCliConfig = New-Object PSObject }
    $taskPluginPaths = @($taskCliConfig.cliPluginsExtraDirs | Where-Object { $_ })
    if ($taskPluginPaths -notcontains $taskPluginDirectory) {
        $taskPluginPaths += $taskPluginDirectory
        $taskCliConfig | Add-Member -MemberType NoteProperty -Name cliPluginsExtraDirs -Value $taskPluginPaths -Force
        [IO.File]::WriteAllText($taskCliConfigPath, ($taskCliConfig | ConvertTo-Json -Depth 8),
            (New-Object System.Text.UTF8Encoding($false)))
    }
}
$taskDockerArgs = @('--config', $taskDockerConfigDirectory)
$taskComposeArgs = @('compose', '--project-name', 'afterform', '--project-directory', $taskRoot,
    '--env-file', (Join-Path $taskRoot '.local\docker\compose.env'),
    '--file', (Join-Path $taskRoot 'compose.yaml'))

function ConvertTo-AfterformArgument([string]$Argument) {
    $taskEscaped = [regex]::Replace($Argument, '(\\*)"', '$1$1\"')
    $taskEscaped = [regex]::Replace($taskEscaped, '(\\+)$', '$1$1')
    return '"' + $taskEscaped + '"'
}

function Invoke-AfterformDocker {
    param([string[]]$Arguments, [int]$TimeoutSeconds = 15)
    $taskInfo = New-Object System.Diagnostics.ProcessStartInfo
    $taskInfo.FileName = $taskDockerExe
    $taskInfo.Arguments = (($taskDockerArgs + $Arguments) |
        ForEach-Object { ConvertTo-AfterformArgument $_ }) -join ' '
    $taskInfo.WorkingDirectory = $taskRoot
    $taskInfo.UseShellExecute = $false
    $taskInfo.CreateNoWindow = $true
    $taskInfo.RedirectStandardOutput = $true
    $taskInfo.RedirectStandardError = $true
    $taskProcess = [System.Diagnostics.Process]::Start($taskInfo)
    $taskOutput = $taskProcess.StandardOutput.ReadToEndAsync()
    $taskError = $taskProcess.StandardError.ReadToEndAsync()
    try {
        if (-not $taskProcess.WaitForExit($TimeoutSeconds * 1000)) {
            $taskProcess.Kill()
            return @{ ExitCode = -1; Output = ''; Error = "Docker $TimeoutSeconds saniyede yanit vermedi." }
        }
        return @{ ExitCode = $taskProcess.ExitCode; Output = $taskOutput.Result.Trim(); Error = $taskError.Result.Trim() }
    } finally { $taskProcess.Dispose() }
}

function Assert-AfterformDocker($Result, [string]$Message) {
    if ($Result.ExitCode -ne 0) {
        # These calls contain no credential arguments or full inspect output.
        if ($Result.Error) { Write-Host $Result.Error }
        throw $Message
    }
}

function Assert-AfterformProjectOwnership {
    $taskResult = Invoke-AfterformDocker -Arguments @('ps', '--all', '--filter',
        'label=com.docker.compose.project=afterform', '--format', '{{.ID}}')
    Assert-AfterformDocker $taskResult 'Compose proje sahipligi okunamadi.'
    foreach ($taskId in @($taskResult.Output -split '\r?\n' | Where-Object { $_ })) {
        $taskLabel = Invoke-AfterformDocker -Arguments @('inspect', '--format',
            '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}', $taskId)
        Assert-AfterformDocker $taskLabel 'Mevcut Compose konteyneri okunamadi.'
        $taskExpected = $taskRoot.Replace('\', '/').TrimEnd('/')
        if ($taskLabel.Output.Replace('\', '/').TrimEnd('/') -ine $taskExpected) {
            throw 'afterform Compose adi baska bir calisma klasorunde kullaniliyor; mevcut konteynere dokunulmadi.'
        }
    }
}
