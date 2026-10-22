param([switch]$RefreshShell)
$ErrorActionPreference = 'Stop'
# Only disposable hosted VMs: never change a developer's desktop or services.
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or !$env:RUNNER_TEMP) {
    throw 'IME desktop preparation is restricted to disposable GitHub-hosted runners.'
}
Start-Transcript -Path (Join-Path $env:RUNNER_TEMP 'v3-ime-desktop.txt')
try {
    $taskSession = (Get-Process -Id $PID).SessionId
    Write-Output "Disposable input session: $taskSession"
    # Windows 11 hosted images may display first-logon privacy setup and keep
    # focus away from the fixture. Use the documented managed-device policy;
    # do not click Accept or enable optional data collection for this test.
    # https://learn.microsoft.com/windows/client-management/mdm/policy-csp-privacy#disableprivacyexperience
    $taskOobeKey = 'HKLM:/SOFTWARE/Policies/Microsoft/Windows/OOBE'
    if (!(Test-Path $taskOobeKey)) { New-Item -Path $taskOobeKey -Force | Out-Null }
    New-ItemProperty -Path $taskOobeKey -Name DisablePrivacyExperience -Value 1 -PropertyType DWord -Force | Out-Null
    foreach ($taskProcessName in @('CloudExperienceHost', 'CloudExperienceHostBroker', 'UserOOBEBroker')) {
        Get-Process -Name $taskProcessName -ErrorAction SilentlyContinue |
            Where-Object SessionId -eq $taskSession | ForEach-Object {
                Write-Output "Closing disposable first-logon experience: $($_.ProcessName)"
                Stop-Process -Id $_.Id -ErrorAction Stop
            }
    }
    if ($RefreshShell -and (Get-CimInstance Win32_OperatingSystem).ProductType -eq 1) {
        # Apply the first-logon policy to the disposable shell that owns the
        # observed Shell_OOBEProxy window. No user account is signed out.
        Get-Process -Name explorer -ErrorAction SilentlyContinue |
            Where-Object SessionId -eq $taskSession | Stop-Process
        Start-Process -FilePath "$env:WINDIR/explorer.exe" -WindowStyle Hidden
        Start-Sleep -Seconds 3
    }
    # Record framework readiness separately from IME compatibility. Microsoft
    # documents this service as required for IME mode/input-method switching.
    foreach ($taskServiceName in @('TextInputManagementService', 'TabletInputService')) {
        $taskService = Get-Service -Name $taskServiceName -ErrorAction SilentlyContinue
        if ($taskService) {
            Write-Output "Input service $taskServiceName : $($taskService.Status), $($taskService.StartType)"
            if ($taskService.StartType -eq 'Disabled') { Set-Service -Name $taskServiceName -StartupType Manual }
            if ($taskService.Status -ne 'Running') { Start-Service -Name $taskServiceName }
        }
    }
    Start-Process -FilePath "$env:WINDIR/System32/ctfmon.exe" -WindowStyle Hidden
    Get-Process -Name ctfmon, TextInputHost, imebroker, CloudExperienceHost, CloudExperienceHostBroker, UserOOBEBroker -ErrorAction SilentlyContinue |
        Where-Object SessionId -eq $taskSession | Select-Object ProcessName, Id, SessionId | Format-Table | Out-String | Write-Output
    Get-CimInstance Win32_Process | Where-Object { $_.SessionId -eq $taskSession -and $_.Name -match 'oobe|experience|wwahost|explorer' } |
        Select-Object Name, ProcessId, ExecutablePath | Format-Table -AutoSize | Out-String -Width 220 | Write-Output
} finally { Stop-Transcript }
