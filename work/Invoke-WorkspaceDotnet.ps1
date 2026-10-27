param([Parameter(ValueFromRemainingArguments=$true)][string[]]$DotnetArguments)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskRunInfo = [System.Diagnostics.ProcessStartInfo]::new()
$taskRunInfo.FileName = (Get-Command dotnet).Source
$taskRunInfo.WorkingDirectory = $taskRoot
$taskRunInfo.UseShellExecute = $false
$taskRunInfo.Environment['APPDATA'] = Join-Path $taskRoot '.local\dotnet-appdata'
$taskRunInfo.Environment['DOTNET_CLI_HOME'] = Join-Path $taskRoot '.local\dotnet-home'
$taskRunInfo.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
$taskRunInfo.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = '0'
$taskRunInfo.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE'] = 'false'
foreach ($taskArgument in $DotnetArguments) { $taskRunInfo.ArgumentList.Add($taskArgument) }
$taskProcess = [System.Diagnostics.Process]::Start($taskRunInfo)
$taskProcess.WaitForExit()
exit $taskProcess.ExitCode

