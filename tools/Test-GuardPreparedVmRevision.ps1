param([ValidateRange(1, 10)][int]$Minutes = 10)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:GITHUB_REPOSITORY -ne '31916/ImeLayoutRouter') {
    throw 'Revision validation is restricted to this repository disposable GitHub-hosted VM.'
}
# Explicitly enabled by workflow_dispatch only. Keep the prepared VM briefly
# available for a new V3 commit, retaining the exact revision and each result.
# Never retry unchanged code or install anything on a developer workstation.
$taskDeadline = [DateTime]::UtcNow.AddMinutes($Minutes)
$taskRevision = git rev-parse HEAD
Write-Output "Prepared VM repair window: $Minutes minutes; initial revision $taskRevision"
while ([DateTime]::UtcNow -lt $taskDeadline) {
    Start-Sleep -Seconds 20
    git fetch --no-tags --depth=1 origin refs/heads/V3
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the authorized V3 branch.' }
    $taskNext = git rev-parse FETCH_HEAD
    if ($taskNext -eq $taskRevision) { continue }
    git checkout --detach --quiet $taskNext
    if ($LASTEXITCODE -ne 0) { throw 'Could not check out the V3 validation revision.' }
    $taskRevision = $taskNext
    $taskRevisionStarted = [DateTime]::UtcNow
    $taskEvidence = Join-Path $env:RUNNER_TEMP "v3-repairs/$taskRevision"
    New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
    Write-Output "Validating prepared VM revision $taskRevision"
    dotnet build tests/GuardImeTests/GuardImeTests.csproj -c Release -p:Edition=Guard --warnaserror
    $taskBuild = $LASTEXITCODE
    $taskBrowser = -1; $taskNative = -1
    if ($taskBuild -eq 0) {
        ./tools/Initialize-GuardTestDesktop.ps1
        dotnet tests/GuardImeTests/bin/Guard/Release/net8.0-windows/GuardImeTests.dll --browser-only
        $taskBrowser = $LASTEXITCODE
        ./tools/Initialize-GuardTestDesktop.ps1
        dotnet tests/GuardImeTests/bin/Guard/Release/net8.0-windows/GuardImeTests.dll
        $taskNative = $LASTEXITCODE
        # Do not carry a previous revision's failure screenshot into a later
        # passing revision. Provisioning evidence is retained by the first upload.
        Get-ChildItem -LiteralPath $env:RUNNER_TEMP -File | Where-Object {
            $_.Name -like 'v3-ime-*' -and $_.LastWriteTimeUtc -ge $taskRevisionStarted
        } |
            Copy-Item -Destination $taskEvidence
    }
    $taskResult = "revision=$taskRevision`nbuild=$taskBuild`nbrowser=$taskBrowser`nfallback=$taskNative"
    $taskResult | Set-Content -LiteralPath (Join-Path $taskEvidence 'revision.txt')
    Write-Output $taskResult
    if ($taskBuild -eq 0 -and $taskBrowser -eq 0 -and $taskNative -eq 0) {
        'passed=true' | Add-Content -LiteralPath $env:GITHUB_OUTPUT
        "revision=$taskRevision" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
        exit 0
    }
}
throw 'Prepared VM repair window ended without a passing new revision. Initial failures remain recorded.'
