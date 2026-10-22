$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or !$env:RUNNER_TEMP) {
    throw 'Google Japanese IME provisioning is restricted to disposable GitHub-hosted Windows runners.'
}
# Pinned official Google distribution, verified against Microsoft's WinGet manifest:
# https://github.com/microsoft/winget-pkgs/blob/master/manifests/g/Google/JapaneseIME/3.34.6260.0/Google.JapaneseIME.installer.yaml
$taskInstaller = Join-Path $env:RUNNER_TEMP 'GoogleJapaneseInput64.msi'
Invoke-WebRequest 'https://dl.google.com/release2/misc/adpwg6fqrqckyq2txworca5desoq_3.34.6260.0/GoogleJapaneseInput64-3.34.6260.0.msi' -OutFile $taskInstaller
if ((Get-FileHash $taskInstaller -Algorithm SHA256).Hash -ne '41F66033E34E10DC5420B68A78D8C1F25E86AC4077AF34264ED3E3C25647F0A1') { throw 'IME installer hash mismatch.' }
$taskSignature = Get-AuthenticodeSignature $taskInstaller
if ($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'O=Google (LLC|Inc\.)') { throw 'Valid Google signature is required.' }
Write-Output "Verified Google Japanese IME 3.34.6260.0: $($taskSignature.Status)"
$taskInstallerLog = Join-Path $env:RUNNER_TEMP 'v3-ime-google-install.txt'
$taskInstallerProcess = Start-Process msiexec.exe -WindowStyle Hidden -ArgumentList @('/i', ('"' + $taskInstaller + '"'), '/qn', '/norestart', '/L*v', ('"' + $taskInstallerLog + '"')) -PassThru
if (!$taskInstallerProcess.WaitForExit(180000)) { throw 'Disposable VM IME installation timed out.' }
if ($taskInstallerProcess.ExitCode -ne 0) { throw "Google IME installer exit code: $($taskInstallerProcess.ExitCode)" }
$taskLanguages = Get-WinUserLanguageList
if ('ja-JP' -notin $taskLanguages.LanguageTag) { $taskLanguages.Add('ja-JP') }
$taskJapanese = $taskLanguages | Where-Object LanguageTag -eq 'ja-JP'
$taskTip = '0411:{D5A86FD5-5308-47EA-AD16-9C4EB160EC3C}{773EB24E-CA1D-4B1B-B420-FA985BB0B80D}'
if ($taskTip -notin $taskJapanese.InputMethodTips) { $taskJapanese.InputMethodTips.Add($taskTip) }
Set-WinUserLanguageList -LanguageList $taskLanguages -Force
Start-Process -FilePath "$env:WINDIR/System32/ctfmon.exe" -WindowStyle Hidden
Write-Output 'Google Japanese input registered on the disposable VM. Actual input is tested separately.'
