$ErrorActionPreference = 'Stop'
$taskDisk = Get-Volume | Where-Object FileSystemLabel -eq 'V3TEST' | Select-Object -First 1
if (!$taskDisk) { throw 'Disposable VM payload disk missing' }
$taskSource = "$($taskDisk.DriveLetter):\"
$taskConfig = Get-Content -Raw -LiteralPath (Join-Path $taskSource 'config.json') | ConvertFrom-Json
if ((Get-CimInstance Win32_ComputerSystem).Model -notmatch 'QEMU|Standard PC') { throw 'Nested QEMU VM required' }
New-Item -ItemType Directory -Path C:/V3Fixture -Force | Out-Null
Copy-Item -Path "$taskSource*" -Destination C:/V3Fixture -Recurse -Force
Set-Location C:/V3Fixture
$taskEvidence = 'C:/V3Fixture/evidence'
New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
$env:GITHUB_ACTIONS = 'true'
$env:RUNNER_ENVIRONMENT = 'github-hosted'
$env:RUNNER_TEMP = $taskEvidence
$env:GUARD_TEST_IME_CLSID = '03B5835F-F03C-411B-9CE2-AA23E1171E36'
$env:GUARD_BROWSER_STARTS = '3'
$env:GUARD_TEST_VM_ENDPOINT = $taskConfig.endpoint
$env:GUARD_TEST_VM_TOKEN = $taskConfig.token
function Report([string]$Phase) {
    Invoke-RestMethod -Method Post -Uri "$($taskConfig.endpoint)/status" -Headers @{Authorization=$taskConfig.token} -ContentType application/json -Body (@{phase=$Phase} | ConvertTo-Json -Compress) | Out-Null
}
Start-Transcript -Path "$taskEvidence/guest.txt"
$taskExit = 1
try {
    Report 'booted'
    Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, OSArchitecture
    powercfg /a
    ./Initialize-GuardTestDesktop.ps1 -RefreshShell
    Report 'installing-browser'
    $taskChrome = "$taskEvidence/chrome.msi"
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi' -OutFile $taskChrome
    $taskSignature = Get-AuthenticodeSignature $taskChrome
    if ($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'O=Google LLC') { throw 'Official signed Google Chrome installer required' }
    $taskInstall = Start-Process msiexec.exe -ArgumentList @('/i', $taskChrome, '/qn', '/norestart') -WindowStyle Hidden -PassThru -Wait
    if ($taskInstall.ExitCode -ne 0) { throw "Chrome installation failed: $($taskInstall.ExitCode)" }
    Report 'native-checks'
    & ./native/GuardTests.exe *> "$taskEvidence/policy.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Policy/settings checks failed' }
    Report 'browser-input'
    & ./ime/GuardImeTests.exe --browser-only *> "$taskEvidence/browser-console.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Browser input checks failed' }
    Report 'composition-input'
    & ./ime/GuardImeTests.exe *> "$taskEvidence/composition-console.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Composition input checks failed' }
    $taskExit = 0
} catch {
    $_ | Out-String | Write-Output
} finally {
    "exit=$taskExit" | Set-Content "$taskEvidence/result.txt"
    Stop-Transcript
    # Send only fixed test logs/screenshots, never downloaded installers or VM secrets.
    $taskFiles = Get-ChildItem -LiteralPath $taskEvidence -File | Where-Object Extension -in '.txt', '.png'
    Compress-Archive -LiteralPath $taskFiles.FullName -DestinationPath C:/V3Fixture/result.zip -Force
    Invoke-RestMethod -Method Post -Uri "$($taskConfig.endpoint)/result" -Headers @{Authorization=$taskConfig.token} -ContentType application/zip -InFile C:/V3Fixture/result.zip | Out-Null
    Report "finished-$taskExit"
}
