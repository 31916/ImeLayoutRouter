$ErrorActionPreference = 'Stop'
# This script intentionally changes language configuration only on an ephemeral
# GitHub-hosted runner. Never invoke it on a developer or self-hosted machine.
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or !$env:RUNNER_TEMP) {
    throw 'Japanese IME provisioning is restricted to disposable GitHub-hosted Windows runners.'
}
$taskEvidence = Join-Path $env:RUNNER_TEMP 'v3-ime-provisioning.txt'
Start-Transcript -Path $taskEvidence
try {
    Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, OSArchitecture
    Get-Process -Id $PID | Select-Object SessionId
    foreach ($taskFeature in @('Language.Basic~~~ja-JP~0.0.1.0', 'Language.Fonts.Jpan~~~und-JPAN~0.0.1.0')) {
        $taskCapability = Get-WindowsCapability -Online -Name $taskFeature
        if (!$taskCapability) { throw "Required input capability is unavailable: $taskFeature" }
        $taskCapability | Select-Object Name, State
        if ($taskCapability.State -ne 'Installed') {
            $taskInstall = Add-WindowsCapability -Online -Name $taskFeature
            $taskInstall | Select-Object Online, RestartNeeded
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
