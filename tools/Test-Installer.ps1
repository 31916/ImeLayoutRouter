param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][ValidateSet('Simple', 'Full')][string]$Edition,
    [Parameter(Mandatory)][string]$TestRoot,
    [string]$PreviousInstaller
)
$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$previousInstallerPath = if ($PreviousInstaller) { (Resolve-Path -LiteralPath $PreviousInstaller).Path } else { $null }
$root = [IO.Path]::GetFullPath($TestRoot)
$installDirectory = [IO.Path]::GetFullPath((Join-Path $root $Edition))
if (!$installDirectory.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installation must stay inside the test root.'
}
$editionName = if ($Edition -eq 'Simple') { 'V1' } else { 'V2' }
$expectedVersion = if ($Edition -eq 'Simple') { '1.1.3' } else { '2.0.3' }
$expectedIconHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot '../Assets/app.ico') -Algorithm SHA256).Hash
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
function Install-TestPackage([string]$phase, [string]$package = $installerPath, [string]$version = $expectedVersion, [string]$tasks = '') {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', ('/TASKS="' + $tasks + '"'),
        ('/DIR="' + $installDirectory + '"'), ('/LOG="' + (Join-Path $root "$Edition-$phase.log") + '"'))
    $process = Start-Process -FilePath $package -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode)" }
    if (!(Test-Path -LiteralPath $exe)) { throw 'Installed executable missing.' }
    $registration = Get-ItemProperty -LiteralPath $uninstallKey
    if ($registration.InstallLocation.TrimEnd('\') -ne $installDirectory) { throw 'Unexpected installation registration.' }
    if (!(Get-Item -LiteralPath $exe).VersionInfo.ProductName.Contains($editionName)) { throw 'Wrong product edition in executable.' }
    if ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion.Split('+')[0] -ne $version) { throw 'Wrong executable version.' }
    if ($registration.DisplayVersion -ne $version) { throw 'Wrong installer version.' }
    if ((Get-FileHash -LiteralPath (Join-Path $installDirectory 'Assets/app.ico') -Algorithm SHA256).Hash -ne $expectedIconHash) { throw 'Original application icon was changed.' }
    Write-Output "PASS $Edition $phase and product metadata"
}
function Uninstall-TestPackage {
    $process = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
    for ($attempt = 0; $attempt -lt 50 -and (Test-Path -LiteralPath $exe); $attempt++) { Start-Sleep -Milliseconds 100 }
    if ((Test-Path -LiteralPath $exe) -or (Test-Path -LiteralPath $uninstallKey)) { throw 'Uninstall did not remove executable and registration.' }
}
try {
    $runKey.DeleteValue('ImeLayoutRouter', $false)
    if ($previousInstallerPath) {
        $previousVersion = if ($Edition -eq 'Simple') { '1.1.2' } else { '2.0.2' }
        Install-TestPackage 'previous-version' $previousInstallerPath $previousVersion
        $ownCommand = '"' + $exe + '"'
        $runKey.SetValue('ImeLayoutRouter', $ownCommand, [Microsoft.Win32.RegistryValueKind]::String)
        Install-TestPackage 'upgrade-from-previous'
        if ($runKey.GetValue('ImeLayoutRouter') -ne $ownCommand) { throw 'Upgrade changed own startup registration.' }
        if ((Get-SettingsFingerprint) -ne $settingsBefore) { throw 'Upgrade changed existing settings.' }
        Uninstall-TestPackage
        Write-Output "PASS $Edition upgrade from $previousVersion to $expectedVersion"
    }
    Install-TestPackage 'install'
    if (Test-Path -LiteralPath (Join-Path $installDirectory 'numeric-host.json')) { throw 'Fresh installation enabled browser integration without a user choice.' }
    if ($null -ne $runKey.GetValue('ImeLayoutRouter')) { throw 'Fresh installation enabled startup without an existing opt-in.' }
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

    # Reproduce the reported state: Run still targets the prototype's original
    # directory, even if that directory is missing or contains incomplete files.
    $prototypeExe = Join-Path $env:LOCALAPPDATA 'Programs\IME Layout Router\ImeLayoutRouter.exe'
    $runKey.SetValue('ImeLayoutRouter', '"' + $prototypeExe + '"', [Microsoft.Win32.RegistryValueKind]::String)
    Install-TestPackage 'prototype-startup-migration'
    if ($runKey.GetValue('ImeLayoutRouter') -ne ('"' + $exe + '"')) { throw 'Prototype startup was not migrated to the installed executable.' }
    Uninstall-TestPackage
    if ($null -ne $runKey.GetValue('ImeLayoutRouter')) { throw 'Migrated startup registration survived uninstall.' }
    Write-Output "PASS $Edition prototype startup migration and uninstall cleanup"

    $otherCommand = '"C:\IME-test-other-edition\ImeLayoutRouter.exe"'
    $runKey.SetValue('ImeLayoutRouter', $otherCommand, [Microsoft.Win32.RegistryValueKind]::String)
    Install-TestPackage 'cross-edition-cleanup'
    if ($runKey.GetValue('ImeLayoutRouter') -ne $otherCommand) { throw 'Installation replaced another edition startup registration.' }
    Uninstall-TestPackage
    if ($runKey.GetValue('ImeLayoutRouter') -ne $otherCommand) { throw 'Uninstall removed another edition startup registration.' }
    if ((Get-SettingsFingerprint) -ne $settingsBefore) { throw 'Installer lifecycle changed existing settings.' }
    Write-Output "PASS $Edition uninstall preserves other edition startup and existing settings"
    if ($env:GITHUB_ACTIONS -eq 'true' -and $env:RUNNER_ENVIRONMENT -eq 'github-hosted') {
        Install-TestPackage 'numeric-companion' -tasks 'numericbrowser'
        $manifestPath = Join-Path $installDirectory 'numeric-host.json'
        $hostKeys = @('Google\Chrome','Microsoft\Edge') | ForEach-Object { "Software\$_\NativeMessagingHosts\ch.imelayoutrouter.numeric" }
        $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        if ($manifest.path -ne 'ImeLayoutRouter.exe' -or @($manifest.allowed_origins).Count -ne 1 -or
            $manifest.allowed_origins[0] -ne 'chrome-extension://feaapoghfchepkmogdmdkcedfimopbpl/' -or
            !(Test-Path -LiteralPath (Join-Path $installDirectory 'browser-extension/manifest.json'))) { throw 'Optional companion files do not match the browser registration.' }
        foreach ($key in $hostKeys) {
            if ([Microsoft.Win32.Registry]::GetValue("HKEY_CURRENT_USER\$key",'',$null) -ne $manifestPath) { throw 'Optional host registration missing.' }
        }
        $peerEdition = if ($Edition -eq 'Simple') { 'V2' } else { 'V1' }
        $peerManifest = Join-Path $root 'peer-host.json'
        Copy-Item -LiteralPath $manifestPath -Destination $peerManifest
        $records = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\31916\IMELayoutRouter\NumericHost',$true)
        try {
            $records.SetValue($peerEdition, $peerManifest)
            Uninstall-TestPackage
            foreach ($key in $hostKeys) {
                if ([Microsoft.Win32.Registry]::GetValue("HKEY_CURRENT_USER\$key",'',$null) -ne $peerManifest) { throw 'Uninstall did not preserve the other enabled edition.' }
            }
            if ($null -ne $records.GetValue($editionName)) { throw 'Uninstall kept this edition registered.' }
        } finally {
            $records.DeleteValue($peerEdition,$false)
            $records.Dispose()
            foreach ($key in $hostKeys) {
                if ([Microsoft.Win32.Registry]::GetValue("HKEY_CURRENT_USER\$key",'',$null) -eq $peerManifest) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($key,$false) }
            }
        }
        Write-Output "PASS $Edition optional numeric companion install, original extension origin and cross-edition uninstall handoff"
    }
} finally {
    try { if (Test-Path -LiteralPath $uninstaller) { Uninstall-TestPackage } }
    finally {
        if ($null -ne $startupBefore) { $runKey.SetValue('ImeLayoutRouter', $startupBefore, $startupKind) }
        else { $runKey.DeleteValue('ImeLayoutRouter', $false) }
        $runKey.Dispose()
    }
}
