# CI only: never install test packages or change startup on a developer's PC.
param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or !$env:RUNNER_TEMP) { throw 'This installer test is restricted to the disposable GitHub-hosted runner.' }
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class GuardInstallerWindow {
    private delegate bool Callback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int count);
    public static bool SettingsExist(int process) {
        bool found=false;
        EnumWindows((window, _) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner != process) return true;
            var text=new StringBuilder(256); GetWindowTextW(window,text,text.Capacity);
            if (text.ToString().StartsWith("IME Layout Router V3",StringComparison.Ordinal)) found=true;
            return !found;
        },IntPtr.Zero);
        return found;
    }
}
'@
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
    # The initial settings dialog must not trap --shutdown before the tray's
    # application loop starts. This is the real packaged process in the VM.
    $taskFirstRun = Start-Process -FilePath $executable -ArgumentList '--first-run' -WindowStyle Hidden -PassThru
    try {
        $taskReadyBy = [Environment]::TickCount64 + 10000
        do { if ([GuardInstallerWindow]::SettingsExist($taskFirstRun.Id)) { break }; Start-Sleep -Milliseconds 100 }
        while (!$taskFirstRun.HasExited -and [Environment]::TickCount64 -lt $taskReadyBy)
        if ($taskFirstRun.HasExited -or ![GuardInstallerWindow]::SettingsExist($taskFirstRun.Id)) { throw 'Packaged first-run settings did not open.' }
        $taskStop = Start-Process -FilePath $executable -ArgumentList '--shutdown' -WindowStyle Hidden -PassThru
        try {
            if (!$taskStop.WaitForExit(10000) -or $taskStop.ExitCode -ne 0 -or !$taskFirstRun.WaitForExit(5000)) {
                throw 'Packaged first-run settings prevented graceful shutdown.'
            }
        } finally { $taskStop.Dispose() }
        Write-Output 'PASS Packaged first-run settings close through graceful shutdown.'
    } finally {
        # Only this throwaway, still-unsaved first-run process; suppression was
        # never enabled. Cleanup cannot target an existing user application.
        if (!$taskFirstRun.HasExited) { $taskFirstRun.Kill($true); $taskFirstRun.WaitForExit(5000) | Out-Null }
        $taskFirstRun.Dispose()
    }
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
    # An unreadable restoration record must stop both replacement and removal,
    # retaining the installed executable and the record for investigation.
    $recoveryDirectory = Join-Path $env:LOCALAPPDATA 'ImeLayoutRouter/v3-recovery'
    [IO.Directory]::CreateDirectory($recoveryDirectory) | Out-Null
    $blockedRecord = Join-Path $recoveryDirectory ([Guid]::NewGuid().ToString('N') + '.json')
    [IO.File]::WriteAllText($blockedRecord, '{')
    try {
        foreach ($taskBlockedOperation in @(
            @($Installer, @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=""', "/DIR=`"$testRoot`"")),
            @((Join-Path $testRoot 'unins000.exe'), @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'))
        )) {
            $blocked = Start-Process -FilePath $taskBlockedOperation[0] -ArgumentList $taskBlockedOperation[1] -WindowStyle Hidden -PassThru -Wait
            try {
                if ($blocked.ExitCode -eq 0 -or !(Test-Path -LiteralPath $executable) -or !(Test-Path -LiteralPath $blockedRecord)) {
                    throw 'Installer proceeded despite an unresolved display-restoration record.'
                }
            } finally { $blocked.Dispose() }
        }
        Write-Output 'PASS Upgrade and uninstall abort safely when display recovery is incomplete.'
    } finally { Remove-Item -LiteralPath $blockedRecord }
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
        Invoke-Setup $executable @('--first-run')
        $taskReadyBy = [Environment]::TickCount64 + 10000
        while (![GuardInstallerWindow]::SettingsExist($app.Id) -and [Environment]::TickCount64 -lt $taskReadyBy) {
            Start-Sleep -Milliseconds 100
        }
        if (![GuardInstallerWindow]::SettingsExist($app.Id)) { throw 'Running V3 did not reopen its settings.' }
        Invoke-Setup $Installer @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=""', "/DIR=`"$testRoot`"")
        if (!$app.WaitForExit(5000)) { throw 'Upgrade did not stop V3 through its restoration path.' }
        if ((Get-StartupValue 'ImeLayoutRouterV3') -ne ('"' + $executable + '"') -or
            (Get-Content -Raw -LiteralPath $settingsFile) -ne '{"Version":1,"Enabled":true}') { throw 'Upgrade changed the saved V3 choices.' }
        $app.Dispose()
        $app = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
        Start-Sleep -Milliseconds 1000
        if ($app.HasExited) { throw 'Upgraded V3 did not remain running.' }
        Invoke-Setup (Join-Path $testRoot 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
        if (!$app.WaitForExit(5000)) { throw 'Uninstall left V3 running.' }
    } finally {
        $app.Dispose()
        Remove-Item -LiteralPath $settingsFile -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $executable) { throw 'Application was not uninstalled.' }
    if (Get-StartupValue 'ImeLayoutRouterV3') { throw 'V3 owned startup was not removed.' }
    if ((Get-StartupValue 'ImeLayoutRouter') -ne 'preserved V2 startup') { throw 'Uninstall touched V2 startup.' }
    Write-Output 'PASS V3 installer, original icon, V3 documentation, packaged helper, upgrade/uninstall while running, retained choices and V2 startup isolation.'
} finally {
    foreach ($item in @(@('ImeLayoutRouter', $v2Backup), @('ImeLayoutRouterV3', $v3Backup))) {
        if ($null -eq $item[1]) { Remove-ItemProperty -Path $runKey -Name $item[0] -ErrorAction SilentlyContinue }
        else { Set-ItemProperty -Path $runKey -Name $item[0] -Value $item[1] }
    }
}
