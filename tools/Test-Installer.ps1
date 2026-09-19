param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][ValidateSet('Simple', 'Full')][string]$Edition,
    [Parameter(Mandatory)][string]$TestRoot
)
$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$root = [IO.Path]::GetFullPath($TestRoot)
$installDirectory = [IO.Path]::GetFullPath((Join-Path $root $Edition))
if (!$installDirectory.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installation must stay inside the test root.'
}
$editionName = if ($Edition -eq 'Simple') { 'V1' } else { 'V2' }
$appId = if ($Edition -eq 'Simple') { '{CB27A979-27AB-40E2-A613-52CC5EFCB128}' } else { '{62EBC270-35B4-4E79-A170-426D38FEAA8C}' }
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\${appId}_is1"
if (Test-Path -LiteralPath $uninstallKey) { throw "$editionName is already installed; refusing to replace its registration." }
if (Test-Path -LiteralPath $installDirectory) { throw 'Use a fresh test directory.' }
if (Get-Process ImeLayoutRouter -ErrorAction SilentlyContinue) { throw 'Exit the running application before lifecycle tests.' }
New-Item -ItemType Directory -Path $root -Force | Out-Null
$runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
$startupBefore = $runKey.GetValue('ImeLayoutRouter', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
$startupKind = if ($null -ne $startupBefore) { $runKey.GetValueKind('ImeLayoutRouter') } else { $null }
$settingsDirectory = Join-Path $env:LOCALAPPDATA 'ImeLayoutRouter'
function Get-SettingsFingerprint {
    @(Get-ChildItem -LiteralPath $settingsDirectory -File -ErrorAction SilentlyContinue |
      Sort-Object Name | ForEach-Object { $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }) -join "`n"
}
$settingsBefore = Get-SettingsFingerprint
$exe = Join-Path $installDirectory 'ImeLayoutRouter.exe'
$uninstaller = Join-Path $installDirectory 'unins000.exe'
function Install-TestPackage([string]$phase) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/TASKS=""',
        ('/DIR="' + $installDirectory + '"'), ('/LOG="' + (Join-Path $root "$Edition-$phase.log") + '"'))
    $process = Start-Process -FilePath $installerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode)" }
    if (!(Test-Path -LiteralPath $exe)) { throw 'Installed executable missing.' }
    $registration = Get-ItemProperty -LiteralPath $uninstallKey
    if ($registration.InstallLocation.TrimEnd('\') -ne $installDirectory) { throw 'Unexpected installation registration.' }
    if (!(Get-Item -LiteralPath $exe).VersionInfo.ProductName.Contains($editionName)) { throw 'Wrong product edition in executable.' }
    Write-Output "PASS $Edition $phase and product metadata"
}
function Uninstall-TestPackage {
    $process = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
    for ($attempt = 0; $attempt -lt 50 -and (Test-Path -LiteralPath $exe); $attempt++) { Start-Sleep -Milliseconds 100 }
    if ((Test-Path -LiteralPath $exe) -or (Test-Path -LiteralPath $uninstallKey)) { throw 'Uninstall did not remove executable and registration.' }
}
try {
    Install-TestPackage 'install'
    $installedHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    Install-TestPackage 'reinstall-upgrade'
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $installedHash) { throw 'Reinstallation corrupted the executable.' }
    # Hold the real shared instance mutex: the installed application must signal
    # the existing instance and exit without opening settings or writing files.
    $mutex = [Threading.Mutex]::new($false, 'Local\ImeLayoutRouter.Instance')
    $locked = $mutex.WaitOne(0)
    if (!$locked) { throw 'Another instance appeared during testing.' }
    try {
        $process = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(10000)) { $process.Kill(); throw 'Installed app did not respect the shared instance mutex.' }
        if ($process.ExitCode -ne 0) { throw 'Installed app failed on second-instance startup.' }
    } finally { if ($locked) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
    Write-Output "PASS $Edition installed executable and shared single-instance guard"
    $runKey.SetValue('ImeLayoutRouter', '"' + $exe + '"', [Microsoft.Win32.RegistryValueKind]::String)
    Uninstall-TestPackage
    if ($null -ne $runKey.GetValue('ImeLayoutRouter')) { throw 'Own startup registration survived uninstall.' }
    Write-Output "PASS $Edition uninstall removes its own startup registration"
    Install-TestPackage 'cross-edition-cleanup'
    $otherCommand = '"C:\IME-test-other-edition\ImeLayoutRouter.exe"'
    $runKey.SetValue('ImeLayoutRouter', $otherCommand, [Microsoft.Win32.RegistryValueKind]::String)
    Uninstall-TestPackage
    if ($runKey.GetValue('ImeLayoutRouter') -ne $otherCommand) { throw 'Uninstall removed another edition startup registration.' }
    if ((Get-SettingsFingerprint) -ne $settingsBefore) { throw 'Installer lifecycle changed existing settings.' }
    Write-Output "PASS $Edition uninstall preserves other edition startup and existing settings"
} finally {
    try { if (Test-Path -LiteralPath $uninstaller) { Uninstall-TestPackage } }
    finally {
        if ($null -ne $startupBefore) { $runKey.SetValue('ImeLayoutRouter', $startupBefore, $startupKind) }
        else { $runKey.DeleteValue('ImeLayoutRouter', $false) }
        $runKey.Dispose()
    }
}
