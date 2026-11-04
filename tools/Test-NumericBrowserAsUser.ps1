# Exercise the installed, per-user app and the browser at the same normal Windows
# privilege level. The GitHub Actions agent itself runs elevated.
param([Parameter(Mandatory)][string]$Browser, [Parameter(Mandatory)][string]$InstallDir)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') { throw 'Requires a disposable GitHub-hosted VM.' }
$taskName = 'ImeRouterNumeric-' + [guid]::NewGuid().ToString('N')
$scriptPath = Join-Path $env:RUNNER_TEMP "$taskName.ps1"
$report = Join-Path $env:RUNNER_TEMP "$taskName.log"
function Quote-Literal([string]$s) { "'" + $s.Replace("'","''") + "'" }
$script = @(
    '$ErrorActionPreference = "Stop"'
    "Set-Location -LiteralPath $(Quote-Literal $PWD.Path)"
)
foreach ($name in 'PATH','GITHUB_ACTIONS','RUNNER_ENVIRONMENT','GITHUB_REPOSITORY','RUNNER_TEMP','EDITION','PYTHONUTF8','TEMP','TMP','DOTNET_ROOT') {
    $val = [Environment]::GetEnvironmentVariable($name)
    if ($val) { $script += "[Environment]::SetEnvironmentVariable('$name',$(Quote-Literal $val))" }
}
$script += @(
    '$env:IME_NUMERIC_CI_TRACE="1"'
    'if (([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Fixture unexpectedly elevated" }'
    "& $(Quote-Literal (Get-Command python.exe).Source) -u current/tests/numeric-browser.py '$Browser' 'current/tests/WindowsSmoke/bin/$env:EDITION/Release/net8.0-windows/WindowsSmoke.dll' addon $(Quote-Literal (Join-Path $InstallDir 'ImeLayoutRouter.exe')) '$env:EDITION' *> $(Quote-Literal $report)"
    'exit $LASTEXITCODE'
)
$script | Set-Content -LiteralPath $scriptPath -Encoding utf8
$action = New-ScheduledTaskAction -Execute (Get-Command pwsh.exe).Source -Argument "-NoProfile -NonInteractive -WindowStyle Hidden -File `"$scriptPath`""
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited
try {
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal | Out-Null
    Start-ScheduledTask -TaskName $taskName
    $deadline = (Get-Date).AddMinutes(5)
    do {
        Start-Sleep -Seconds 2
        $state = (Get-ScheduledTask -TaskName $taskName).State
        $info = Get-ScheduledTaskInfo -TaskName $taskName
    } until (($state -ne 'Running' -and $info.LastRunTime.Year -gt 2000) -or (Get-Date) -ge $deadline)
    if (Test-Path $report) { Get-Content -LiteralPath $report -Encoding utf8 }
    if ($state -eq 'Running' -or $info.LastTaskResult -ne 0 -or !(Test-Path $report)) { throw "Standard-user browser test failed ($state/$($info.LastTaskResult))." }
} finally {
    Get-ChildItem -LiteralPath $env:RUNNER_TEMP -Filter 'imelayout-numeric-ci-*.txt' | ForEach-Object { Get-Content -LiteralPath $_.FullName | Select-Object -Last 60 }
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
}
