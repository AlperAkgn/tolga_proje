param(
    [ValidatePattern('^[a-zA-Z0-9._@+\-]{1,64}$')]
    [string]$UserName = 'mira.sol',
    [ValidateLength(1, 120)]
    [string]$DisplayName = 'Mira Sol'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskProject = Join-Path $taskRoot 'backend\Afterform.Api'
$taskDll = Join-Path $taskProject 'bin\Debug\net10.0\Afterform.Api.dll'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.local\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

if (-not (Test-Path (Join-Path $taskRoot '.local\backend.settings.json')) -and
    -not $env:ConnectionStrings__Afterform) {
    throw 'Once yerel MySQL baglantisini hazirla.'
}
Write-Host "Sanatci hesabi: $UserName / $DisplayName"
Write-Host 'Parola 15-128 karakter olmali. Yalnizca bosluk kullanma. Girerken ekranda gorunmez.'
$taskPassword = Read-Host 'Sanatci parolasi' -AsSecureString
$taskConfirmation = Read-Host 'Parolayi tekrar gir' -AsSecureString
$taskPasswordPtr = [IntPtr]::Zero
$taskConfirmationPtr = [IntPtr]::Zero
$taskPayload = $null
try {
    $taskPasswordPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskPassword)
    $taskConfirmationPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskConfirmation)
    $taskPlainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPasswordPtr)
    $taskPlainConfirmation = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskConfirmationPtr)
    if ($taskPlainPassword -cne $taskPlainConfirmation) { throw 'Parolalar ayni degil.' }
    if ($taskPlainPassword.Length -lt 15 -or $taskPlainPassword.Length -gt 128 -or
        [string]::IsNullOrWhiteSpace($taskPlainPassword)) { throw 'Parola 15-128 karakter olmali ve yalnizca bosluk olmamali.' }
    $taskPayload = @{ UserName = $UserName; DisplayName = $DisplayName; Password = $taskPlainPassword } |
        ConvertTo-Json -Compress
} finally {
    if ($taskPasswordPtr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPasswordPtr) }
    if ($taskConfirmationPtr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskConfirmationPtr) }
    $taskPassword.Dispose()
    $taskConfirmation.Dispose()
    $taskPlainPassword = $null
    $taskPlainConfirmation = $null
}

Push-Location $taskRoot
try {
    Write-Host 'Backend derleniyor. Calisan eski backend penceresini Ctrl+C ile durdurmus olmalisin.'
    dotnet build $taskProject --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Backend derlenemedi. Calisan backend varsa durdurup tekrar dene.' }

    $taskProcessInfo = New-Object System.Diagnostics.ProcessStartInfo
    $taskProcessInfo.FileName = (Get-Command dotnet).Source
    $taskProcessInfo.Arguments = '"' + $taskDll + '" --provision-artist'
    $taskProcessInfo.WorkingDirectory = $taskProject
    $taskProcessInfo.UseShellExecute = $false
    $taskProcessInfo.CreateNoWindow = $true
    $taskProcessInfo.RedirectStandardInput = $true
    $taskProcessInfo.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $taskProcess = [System.Diagnostics.Process]::Start($taskProcessInfo)
    try {
        # Secret travels only on stdin; it is never a CLI argument or a file.
        $taskInputBytes = [Text.Encoding]::UTF8.GetBytes($taskPayload + [Environment]::NewLine)
        try {
            # Explicit UTF-8 also handles Turkish names/passphrases from PS 5.1.
            $taskProcess.StandardInput.BaseStream.Write($taskInputBytes, 0, $taskInputBytes.Length)
            $taskProcess.StandardInput.BaseStream.Flush()
        } finally { [Array]::Clear($taskInputBytes, 0, $taskInputBytes.Length) }
        $taskProcess.StandardInput.Close()
        $taskPayload = $null
        if (-not $taskProcess.WaitForExit(60000)) {
            $taskProcess.Kill()
            throw 'Hesap olusturma 60 saniyede yanit vermedi. MySQL baglantisini kontrol et.'
        }
        if ($taskProcess.ExitCode -ne 0) { throw 'Sanatci hesabi olusturulamadi; ustteki mesaja bak.' }
    } finally { $taskProcess.Dispose() }
} finally {
    $taskPayload = $null
    Pop-Location
}
