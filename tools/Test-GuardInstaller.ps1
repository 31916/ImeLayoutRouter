# CI only: never install test packages or change startup on a developer's PC.
param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or !$env:RUNNER_TEMP) { throw 'This installer test is restricted to the disposable GitHub runner.' }
$testRoot = Join-Path $env:RUNNER_TEMP ('v3-install-' + [Guid]::NewGuid().ToString('N'))
$runKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
function Get-StartupValue([string]$Name) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try { if ($null -eq $key) { return $null }; return $key.GetValue($Name, $null) }
    finally { if ($null -ne $key) { $key.Dispose() } }
}
$v2Backup = Get-StartupValue 'ImeLayoutRouter'
$v3Backup = Get-StartupValue 'ImeLayoutRouterV3'
function Invoke-Setup([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer process failed: $($process.ExitCode)" }
}
try {
    if (!(Test-Path -LiteralPath $runKey)) { New-Item -Path $runKey | Out-Null }
    Set-ItemProperty -Path $runKey -Name ImeLayoutRouter -Value 'preserved V2 startup'
    Invoke-Setup $Installer @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=""', "/DIR=`"$testRoot`"")
    $executable = Join-Path $testRoot 'ImeLayoutRouter.exe'
    if (!(Test-Path -LiteralPath $executable) -or !(Test-Path -LiteralPath (Join-Path $testRoot 'Assets/app.ico'))) { throw 'Installed application or original icon missing.' }
    $readme = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'README.md')
    if ($readme -notmatch 'V3' -or $readme -notmatch 'not a released') { throw 'V3-specific development documentation missing.' }
    if ((Get-StartupValue 'ImeLayoutRouter') -ne 'preserved V2 startup') { throw 'V2 startup was changed.' }
    if (Get-StartupValue 'ImeLayoutRouterV3') { throw 'Installer enabled startup without a user choice.' }
    # Exercise the real WinExe helper without creating a tray or typing anything.
    $start = [Diagnostics.ProcessStartInfo]::new($executable)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.RedirectStandardInput = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.ArgumentList.Add('--composition-guard-worker')
    $helper = [Diagnostics.Process]::Start($start)
    try {
        Start-Sleep -Milliseconds 600
        if ($helper.HasExited) { throw 'Packaged helper exited while the parent pipe was open.' }
        $helper.StandardInput.Close()
        if (!$helper.WaitForExit(5000) -or $helper.ExitCode -ne 0) { throw 'Packaged helper failed to exit cleanly.' }
    } finally { $helper.Dispose() }
    Set-ItemProperty -Path $runKey -Name ImeLayoutRouterV3 -Value ('"' + $executable + '"')
    $settingsDirectory = Join-Path $env:LOCALAPPDATA 'ImeLayoutRouter'
    $settingsFile = Join-Path $settingsDirectory 'settings-v3.json'
    if (Test-Path -LiteralPath $settingsFile) { throw 'Disposable runner unexpectedly contains existing V3 settings.' }
    [IO.Directory]::CreateDirectory($settingsDirectory) | Out-Null
    [IO.File]::WriteAllText($settingsFile, '{"Version":1,"Enabled":true}')
    $app = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
    try {
        Start-Sleep -Milliseconds 1000
        if ($app.HasExited) { throw 'Installed V3 did not remain running.' }
        Invoke-Setup (Join-Path $testRoot 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
        if (!$app.WaitForExit(5000)) { throw 'Uninstall left V3 running.' }
    } finally {
        $app.Dispose()
        Remove-Item -LiteralPath $settingsFile -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $executable) { throw 'Application was not uninstalled.' }
    if (Get-StartupValue 'ImeLayoutRouterV3') { throw 'V3 owned startup was not removed.' }
    if ((Get-StartupValue 'ImeLayoutRouter') -ne 'preserved V2 startup') { throw 'Uninstall touched V2 startup.' }
    Write-Output 'PASS V3 installer, original icon, V3 documentation, packaged helper, uninstall while running and V2 startup isolation.'
} finally {
    foreach ($item in @(@('ImeLayoutRouter', $v2Backup), @('ImeLayoutRouterV3', $v3Backup))) {
        if ($null -eq $item[1]) { Remove-ItemProperty -Path $runKey -Name $item[0] -ErrorAction SilentlyContinue }
        else { Set-ItemProperty -Path $runKey -Name $item[0] -Value $item[1] }
    }
}
