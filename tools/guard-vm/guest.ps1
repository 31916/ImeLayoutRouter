$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$taskDisk = Get-Volume | Where-Object FileSystemLabel -eq 'V3TEST' | Select-Object -First 1
if (!$taskDisk) { throw 'Disposable VM payload disk missing' }
$taskSource = "$($taskDisk.DriveLetter):\"
$taskConfig = Get-Content -Raw -LiteralPath (Join-Path $taskSource 'config.json') | ConvertFrom-Json
if ((Get-CimInstance Win32_ComputerSystem).Model -notmatch 'QEMU|Standard PC') { throw 'Nested QEMU VM required' }
New-Item -ItemType Directory -Path C:/V3Fixture -Force | Out-Null
if (!(Test-Path -LiteralPath C:/V3Fixture/revision.txt)) {
    Copy-Item -Path "$taskSource*" -Destination C:/V3Fixture -Recurse -Force
}
Set-Location C:/V3Fixture
$taskEvidence = 'C:/V3Fixture/evidence'
New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
$env:GITHUB_ACTIONS = 'true'
$env:RUNNER_ENVIRONMENT = 'github-hosted'
$env:RUNNER_TEMP = $taskEvidence
$env:GUARD_TEST_IME_CLSID = '03B5835F-F03C-411B-9CE2-AA23E1171E36'
$env:GUARD_BROWSER_STARTS = '12'
$env:GUARD_TEST_VM_ENDPOINT = $taskConfig.endpoint
$env:GUARD_TEST_VM_TOKEN = $taskConfig.token
$taskRevision = (Get-Content -Raw C:/V3Fixture/revision.txt).Trim()
if (Test-Path -LiteralPath C:/V3Fixture/bootstrap-revision.txt) {
    $taskRevision = (Get-Content -Raw C:/V3Fixture/bootstrap-revision.txt).Trim()
}
if ($taskRevision -notmatch '^[0-9a-f]{40}$') { throw 'Exact test revision required' }
$taskFixtureRoot = 'C:/V3Fixture'
$taskSdk = $null
function Report([string]$Phase) {
    Invoke-RestMethod -Method Post -Uri "$($taskConfig.endpoint)/status" -Headers @{Authorization=$taskConfig.token} -ContentType application/json -Body (@{phase=$Phase} | ConvertTo-Json -Compress) | Out-Null
}
function Save-Attempt {
    $taskFiles = Get-ChildItem -LiteralPath $taskEvidence -File | Where-Object Extension -in '.txt', '.png'
    Compress-Archive -LiteralPath $taskFiles.FullName -DestinationPath C:/V3Fixture/result.zip -Force
    Invoke-RestMethod -Method Post -Uri "$($taskConfig.endpoint)/result-$taskRevision" -Headers @{Authorization=$taskConfig.token} -ContentType application/zip -InFile C:/V3Fixture/result.zip | Out-Null
}
function Invoke-Fixture([string]$Kind, [string[]]$Arguments, [string]$Log) {
    if ($taskSdk) {
        $taskProject = if ($Kind -eq 'ime') { 'GuardImeTests' } else { 'GuardTests' }
        & $taskSdk "$taskFixtureRoot/tests/$taskProject/bin/Guard/Release/net8.0-windows/$taskProject.dll" @Arguments *> "$taskEvidence/$Log"
    } else {
        $taskExecutable = if ($Kind -eq 'ime') { 'GuardImeTests.exe' } else { 'GuardTests.exe' }
        & "$taskFixtureRoot/$Kind/$taskExecutable" @Arguments *> "$taskEvidence/$Log"
    }
    if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $Kind $Arguments (exit $LASTEXITCODE)" }
}
function Invoke-Stage([string]$Name, [scriptblock]$Action) {
    Report $Name
    try { & $Action } catch {
        $script:taskStageFailures.Add($Name)
        $_ | Out-String | Set-Content "$taskEvidence/failure-$Name.txt"
        Write-Output "Independent validation stage failed: $Name"
        Save-Attempt
    }
}
function Prepare-Repair([string]$Revision) {
    # Public V3 source only. A changed revision is required; failed words are
    # never retried inside a passing attempt. The original evidence is retained.
    if (!$script:taskSdk) {
        if (!(Test-Path -LiteralPath C:/V3Fixture/dotnet/dotnet.exe)) {
            Invoke-WebRequest -UseBasicParsing https://dot.net/v1/dotnet-install.ps1 -OutFile C:/V3Fixture/dotnet-install.ps1
            & C:/V3Fixture/dotnet-install.ps1 -Channel 8.0 -Architecture x64 -InstallDir C:/V3Fixture/dotnet -NoPath
        }
        $script:taskSdk = 'C:/V3Fixture/dotnet/dotnet.exe'
        if (!(Test-Path -LiteralPath $script:taskSdk)) { throw 'Official test SDK installation failed' }
    }
    Invoke-WebRequest -UseBasicParsing "https://codeload.github.com/31916/ImeLayoutRouter/zip/$Revision" -OutFile C:/V3Fixture/repair.zip
    Expand-Archive -LiteralPath C:/V3Fixture/repair.zip -DestinationPath C:/V3Fixture/repairs -Force
    $script:taskFixtureRoot = "C:/V3Fixture/repairs/ImeLayoutRouter-$Revision"
    Push-Location $taskFixtureRoot
    try {
        foreach ($taskProject in @('GuardImeTests', 'GuardTests')) {
            & $taskSdk build "tests/$taskProject/$taskProject.csproj" -c Release -p:Edition=Guard --warnaserror *> "$taskEvidence/build-$taskProject.txt"
            if ($LASTEXITCODE -ne 0) { throw "Repair build failed: $taskProject" }
        }
    } finally { Pop-Location }
}
function Wait-Repair {
    $taskDeadlineFile = "C:/V3Fixture/repair-deadline-$taskRevision.txt"
    if (!(Test-Path -LiteralPath $taskDeadlineFile)) {
        [DateTime]::UtcNow.AddMinutes(10).ToString('o') | Set-Content $taskDeadlineFile
    }
    $taskDeadline = [DateTime]::Parse((Get-Content -Raw $taskDeadlineFile)).ToUniversalTime()
    while ([DateTime]::UtcNow -lt $taskDeadline) {
        $taskRef = Invoke-RestMethod -Uri 'https://api.github.com/repos/31916/ImeLayoutRouter/git/ref/heads/V3' -Headers @{'User-Agent'='IMELayoutRouter-disposable-validation'}
        $taskNext = $taskRef.object.sha
        if ($taskNext -notmatch '^[0-9a-f]{40}$') { throw 'Invalid public V3 revision' }
        if ($taskNext -ne $taskRevision) { return $taskNext }
        Start-Sleep -Seconds 20
    }
    throw 'No corrected revision arrived within the prepared VM window'
}
$taskRebooting = $false
$taskDelegated = $false
$taskTranscriptStopped = $false
Start-Transcript -Path "$taskEvidence/guest.txt" -Append
$taskExit = 1
try {
    Report 'booted'
    # Only fixed, generated fixture observations from prior revisions. Expose
    # the exact baseline/commit evidence while this disposable VM is repairable.
    Get-ChildItem -LiteralPath C:/V3Fixture/evidence -Recurse -File -Filter '*-checks.txt' | ForEach-Object {
        $taskPrior = $_.FullName
        Get-Content -LiteralPath $taskPrior | Where-Object { $_ -match '^Chrome (text|area), enabled=|^System.InvalidOperationException: FAIL' } |
            Select-Object -Last 10 | ForEach-Object { Report ('previous-fixture: ' + $_) }
    }
    Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, OSArchitecture
    if (!(Test-Path -LiteralPath C:/V3Fixture/display-reboot.marker)) {
        $taskCatalog = Get-AuthenticodeSignature C:/V3Fixture/display-driver/qxldod.cat
        if ($taskCatalog.Status -ne 'Valid' -or $taskCatalog.SignerCertificate.Subject -notmatch 'CN=Microsoft Windows Hardware Compatibility Publisher') { throw 'Microsoft-signed QXL display driver required' }
        $taskInf = (Resolve-Path -LiteralPath C:/V3Fixture/display-driver/qxldod.inf).ProviderPath
        pnputil /add-driver $taskInf /install
        if ($LASTEXITCODE -notin 0,3010) { throw 'Signed QXL driver installation failed' }
        # Activate the driver in a fresh interactive logon. The task and account
        # exist only in this gated, disposable VM; no host desktop is involved.
        $taskResumeScript = if ($PSCommandPath.StartsWith($taskSource, [StringComparison]::OrdinalIgnoreCase)) { 'C:\V3Fixture\guest.ps1' } else { $PSCommandPath }
        $taskAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $taskResumeScript + '"')
        $taskPrincipal = New-ScheduledTaskPrincipal -UserId "$env:COMPUTERNAME\V3Test" -LogonType Interactive -RunLevel Highest
        $taskTrigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:COMPUTERNAME\V3Test"
        Register-ScheduledTask -TaskName 'V3DisposableResume' -Action $taskAction -Principal $taskPrincipal -Trigger $taskTrigger -Force | Out-Null
        'Driver installation complete; activation must be verified after reboot.' | Set-Content C:/V3Fixture/display-reboot.marker
        Report 'rebooting-display-driver'
        Stop-Transcript
        $taskTranscriptStopped = $true
        $taskRebooting = $true
        try { Restart-Computer -Force } catch { $taskRebooting = $false; throw }
        return
    }
    Unregister-ScheduledTask -TaskName 'V3DisposableResume' -Confirm:$false -ErrorAction SilentlyContinue
    Get-CimInstance Win32_VideoController | Select-Object Caption, DriverVersion, ConfigManagerErrorCode
    Get-PnpDevice -Class Display | Select-Object Status, FriendlyName, InstanceId
    powercfg /a
    # No sign-in or network access to this guest is exposed outside its isolated
    # hypervisor. Keep only its throwaway fixture session unlocked for S3 tests.
    powercfg /SETACVALUEINDEX SCHEME_CURRENT SUB_NONE CONSOLELOCK 0
    if ($LASTEXITCODE -ne 0) { throw 'Disposable resume configuration failed' }
    powercfg /SETACTIVE SCHEME_CURRENT
    powercfg /change standby-timeout-ac 0
    powercfg /change monitor-timeout-ac 0
    ./Initialize-GuardTestDesktop.ps1 -RefreshShell
    Report 'installing-browser'
    # Windows Installer rejects forward slashes even though PowerShell and
    # MsiOpenPackage can open the same file. Pass canonical native paths.
    $taskChrome = (Resolve-Path -LiteralPath C:/V3Fixture/chrome.msi).ProviderPath
    # Authenticode is required in the preceding Windows build job. Recheck the
    # exact verified bytes here, without changing any guest security settings.
    $taskBrowserHash = (Get-Content -Raw C:/V3Fixture/chrome.sha256).Trim()
    if ($taskBrowserHash -notmatch '^[0-9a-f]{64}$' -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $taskChrome).Hash.ToLowerInvariant() -ne $taskBrowserHash) {
        throw 'Signature-verified browser payload hash mismatch in guest'
    }
    Get-Content C:/V3Fixture/browser-signature.txt
    Report 'installing-signed-browser'
    $taskInstalledMarker = 'C:/V3Fixture/browser-installed.txt'
    if (!(Test-Path -LiteralPath $taskInstalledMarker) -or
        (Get-Content -Raw $taskInstalledMarker).Trim() -ne $taskBrowserHash -or
        !(Test-Path -LiteralPath "$env:ProgramFiles/Google/Chrome/Application/chrome.exe")) {
        $taskInstallLog = [IO.Path]::GetFullPath("$taskEvidence/chrome-install.txt")
        $taskInstall = Start-Process msiexec.exe -ArgumentList @('/i', $taskChrome, '/qn', '/norestart', '/L*v', $taskInstallLog) -WindowStyle Hidden -PassThru
        if (!$taskInstall.WaitForExit(300000)) { throw 'Signed Chrome installation exceeded five minutes; see MSI log' }
        if ($taskInstall.ExitCode -ne 0) { throw "Chrome installation failed: $($taskInstall.ExitCode)" }
        $taskBrowserHash | Set-Content $taskInstalledMarker
    }
    if ($taskRevision -ne (Get-Content -Raw C:/V3Fixture/revision.txt).Trim()) { Prepare-Repair $taskRevision }
    while ($true) {
        $taskEvidence = "C:/V3Fixture/evidence/$taskRevision"
        New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
        $env:RUNNER_TEMP = $taskEvidence
        "revision=$taskRevision" | Set-Content "$taskEvidence/revision.txt"
        Report "attempt-start-$taskRevision"
        try {
            Invoke-Fixture 'ime' @('--configure-vm-display') 'display-setup.txt'
            $script:taskStageFailures = [Collections.Generic.List[string]]::new()
            Invoke-Stage 'native-checks' { Invoke-Fixture 'native' @() 'policy.txt' }
            foreach ($taskBrowser in @('chrome', 'edge')) {
                $env:GUARD_TEST_BROWSER = $taskBrowser
                Invoke-Stage "$taskBrowser-input" {
                    try { Invoke-Fixture 'ime' @('--browser-only') "$taskBrowser-console.txt" } finally {
                        foreach ($taskKind in @('checks.txt', 'failure.png')) {
                            $taskBrowserEvidence = "$taskEvidence/v3-ime-browser-$taskKind"
                            if (Test-Path -LiteralPath $taskBrowserEvidence) {
                                Move-Item -LiteralPath $taskBrowserEvidence -Destination "$taskEvidence/$taskBrowser-$taskKind" -Force
                            }
                        }
                    }
                }
            }
            $env:GUARD_TEST_BROWSER = ''
            Invoke-Stage 'composition-input' {
                $env:GUARD_TEST_VM_SLEEP = '1'
                $env:GUARD_TEST_VM_DISPLAYS = '1'
                Invoke-Fixture 'ime' @() 'composition-console.txt'
            }
            if ($taskStageFailures.Count) { throw ('Validation stages failed: ' + ($taskStageFailures -join ', ')) }
            $taskExit = 0
            'exit=0' | Set-Content "$taskEvidence/result.txt"
            Save-Attempt
            break
        } catch {
            $taskExit = 1
            $_ | Out-String | Set-Content "$taskEvidence/failure.txt"
            'exit=1' | Set-Content "$taskEvidence/result.txt"
            Save-Attempt
            Report "validation-failed-$taskRevision"
            # Re-enter the corrected guest script as well as its test binaries.
            # Setup repairs therefore work in this same prepared VM too.
            throw
        }
    }
} catch {
    $taskExit = 1
    $_ | Out-String | Write-Output
    $_ | Out-String | Set-Content "$taskEvidence/failure.txt"
    if (!$taskTranscriptStopped) { Stop-Transcript; $taskTranscriptStopped = $true }
    'exit=1' | Set-Content "$taskEvidence/result.txt"
    Save-Attempt
    Report "validation-or-setup-failed-$taskRevision"
    try {
        $taskNext = Wait-Repair
        Report "preparing-bootstrap-repair-$taskNext"
        Invoke-WebRequest -UseBasicParsing "https://codeload.github.com/31916/ImeLayoutRouter/zip/$taskNext" -OutFile C:/V3Fixture/bootstrap-repair.zip
        Expand-Archive -LiteralPath C:/V3Fixture/bootstrap-repair.zip -DestinationPath C:/V3Fixture/bootstrap-repairs -Force
        $taskNextScript = "C:\V3Fixture\bootstrap-repairs\ImeLayoutRouter-$taskNext\tools\guard-vm\guest.ps1"
        if (!(Test-Path -LiteralPath $taskNextScript)) { throw 'Corrected VM bootstrap script missing' }
        $taskNext | Set-Content C:/V3Fixture/bootstrap-revision.txt
        Start-Process powershell.exe -WindowStyle Hidden -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $taskNextScript + '"') | Out-Null
        $taskDelegated = $true
    } catch {
        $_ | Out-String | Set-Content "$taskEvidence/repair-failure.txt"
    }
} finally {
    if (!$taskRebooting -and !$taskDelegated) {
    "exit=$taskExit" | Set-Content "$taskEvidence/result.txt"
    if (!$taskTranscriptStopped) { Stop-Transcript }
    if ($taskEvidence -ne 'C:/V3Fixture/evidence') {
        Get-ChildItem -LiteralPath C:/V3Fixture/evidence -File -Filter '*.txt' | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskEvidence ('bootstrap-' + $_.Name))
        }
    }
    # Send only fixed test logs/screenshots, never downloaded installers or VM secrets.
    $taskFiles = Get-ChildItem -LiteralPath $taskEvidence -File | Where-Object Extension -in '.txt', '.png'
    Compress-Archive -LiteralPath $taskFiles.FullName -DestinationPath C:/V3Fixture/result.zip -Force
    Invoke-RestMethod -Method Post -Uri "$($taskConfig.endpoint)/result" -Headers @{Authorization=$taskConfig.token} -ContentType application/zip -InFile C:/V3Fixture/result.zip | Out-Null
    Report "finished-$taskExit"
    }
}
