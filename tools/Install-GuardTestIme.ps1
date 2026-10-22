$ErrorActionPreference = 'Stop'
# This script intentionally changes language configuration only on an ephemeral
# GitHub-hosted runner. Never invoke it on a developer or self-hosted machine.
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or !$env:RUNNER_TEMP) {
    throw 'Japanese IME provisioning is restricted to disposable GitHub-hosted Windows runners.'
}
$taskEvidence = Join-Path $env:RUNNER_TEMP 'v3-ime-provisioning.txt'
Start-Transcript -Path $taskEvidence
try {
    $taskOs = Get-CimInstance Win32_OperatingSystem
    Write-Output "OS: $($taskOs.Caption); version: $($taskOs.Version); architecture: $($taskOs.OSArchitecture)"
    Write-Output "Session: $((Get-Process -Id $PID).SessionId)"
    # runner-images/Configure-System.ps1 blocks Windows Update, including FoD
    # downloads. Permit this explicit capability install on the disposable VM.
    # Keep NoAutoUpdate unchanged: do not initiate a general OS update.
    $taskUpdateKey = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
    if (Test-Path $taskUpdateKey) {
        foreach ($taskSetting in @('DoNotConnectToWindowsUpdateInternetLocations', 'DisableWindowsUpdateAccess')) {
            Set-ItemProperty -Path $taskUpdateKey -Name $taskSetting -Value 0 -Type DWord
        }
    }
    foreach ($taskServiceName in @('wuauserv', 'BITS')) {
        $taskService = Get-Service $taskServiceName
        Write-Output "Download service $taskServiceName : $($taskService.Status), $($taskService.StartType)"
        if ($taskService.StartType -eq 'Disabled') { Set-Service $taskServiceName -StartupType Manual }
        Start-Service $taskServiceName
    }
    # Disposable VMs have no useful LAN peers. Use the documented HTTP-only
    # delivery mode instead of waiting for peer/cache sources.
    $taskDeliveryKey = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization'
    if (!(Test-Path $taskDeliveryKey)) { New-Item -Path $taskDeliveryKey | Out-Null }
    New-ItemProperty -Path $taskDeliveryKey -Name DODownloadMode -Value 0 -PropertyType DWord -Force | Out-Null
    Write-Output 'Delivery Optimization: HTTP-only downloads; peer sharing disabled on this disposable VM.'
    # Basic typing installs the input processor. Supplemental Japanese fonts
    # are not required by this fixture; the hosted image already renders its
    # Japanese test strings (also verified in the independent Google IME run).
    foreach ($taskFeature in @('Language.Basic~~~ja-JP~0.0.1.0')) {
        $taskCapability = Get-WindowsCapability -Online -Name $taskFeature
        if (!$taskCapability) { throw "Required input capability is unavailable: $taskFeature" }
        Write-Output "$taskFeature : $($taskCapability.State)"
        if ($taskCapability.State -ne 'Installed') {
            Write-Output "Installing $taskFeature from Windows Update at $([DateTime]::UtcNow.ToString('O'))"
            $taskInstallJob = Start-Job -ArgumentList $taskFeature -ScriptBlock {
                param($feature)
                $ErrorActionPreference = 'Stop'
                Add-WindowsCapability -Online -Name $feature
            }
            try {
                $taskTimer = [Diagnostics.Stopwatch]::StartNew()
                while ($taskInstallJob.State -in @('NotStarted', 'Running')) {
                    Wait-Job -Job $taskInstallJob -Timeout 30 | Out-Null
                    if ($taskInstallJob.State -notin @('NotStarted', 'Running')) { break }
                    Write-Output "Capability install still pending after $([int]$taskTimer.Elapsed.TotalSeconds) seconds."
                    Get-Content "$env:WINDIR/Logs/CBS/CBS.log" -Tail 80 -ErrorAction SilentlyContinue |
                        Select-String 'WULib DownloadProgress' | Select-Object -Last 1 | ForEach-Object { Write-Output $_.Line }
                    if (Get-Command Get-DeliveryOptimizationStatus -ErrorAction SilentlyContinue) {
                        Get-DeliveryOptimizationStatus -ErrorAction SilentlyContinue |
                            Select-Object Status, FileSize, BytesFromHttp, DownloadMode |
                            Format-List | Out-String | Write-Output
                    }
                    # FoD download can finish before Windows re-services the
                    # language package against the installed cumulative update.
                    if ($taskTimer.Elapsed.TotalMinutes -ge 25) { throw "Capability installation timed out: $taskFeature" }
                }
                $taskInstall = Receive-Job -Job $taskInstallJob -ErrorAction Stop
                if ($taskInstallJob.State -ne 'Completed') { throw 'Capability installation job failed.' }
            } finally {
                if ($taskInstallJob.State -in @('NotStarted', 'Running')) { Stop-Job $taskInstallJob }
                Remove-Job $taskInstallJob
            }
            Write-Output "Installation returned; restart required: $($taskInstall.RestartNeeded)"
            if ($taskInstall.RestartNeeded) { throw 'IME installation requires a restart; this run cannot validate actual input.' }
        }
        if ((Get-WindowsCapability -Online -Name $taskFeature).State -ne 'Installed') { throw "Capability was not installed: $taskFeature" }
    }
    $taskLanguages = Get-WinUserLanguageList
    if ('ja-JP' -notin $taskLanguages.LanguageTag) { $taskLanguages.Add('ja-JP') }
    Set-WinUserLanguageList -LanguageList $taskLanguages -Force
    Get-WinUserLanguageList | Select-Object LanguageTag, InputMethodTips
    Start-Process -FilePath "$env:WINDIR/System32/ctfmon.exe" -WindowStyle Hidden
    Write-Output 'Japanese input provisioned. This is not a composition-test result.'
} finally { Stop-Transcript }
