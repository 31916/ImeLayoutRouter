$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or !$env:RUNNER_TEMP) {
    throw 'Game validation is restricted to disposable GitHub-hosted runners.'
}
$taskRoot = Join-Path $env:RUNNER_TEMP 'v3-openttd'
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
# Official portable packages and SHA-256 published on openttd.org. No account,
# installer, multiplayer connection or third-party game data is used.
$taskPackages = @(
    @('game', 'https://cdn.openttd.org/openttd-releases/15.3/openttd-15.3-windows-win64.zip', '61c0a6a43d81008c7ff4330fb56351bbf66a980cdad041f1d0e08b51f2eeb34c'),
    @('graphics', 'https://cdn.openttd.org/opengfx-releases/8.0/opengfx-8.0-all.zip', '43a0c1dabf39cb865394f3a6cc36d4da5c10ecfaaf55652043104806810903be')
)
foreach ($taskPackage in $taskPackages) {
    $taskZip = Join-Path $taskRoot ($taskPackage[0] + '.zip')
    Invoke-WebRequest $taskPackage[1] -OutFile $taskZip
    if ((Get-FileHash $taskZip -Algorithm SHA256).Hash -ne $taskPackage[2]) { throw 'Official game package hash mismatch' }
    Expand-Archive -LiteralPath $taskZip -DestinationPath (Join-Path $taskRoot $taskPackage[0])
}
$taskExecutables = @(Get-ChildItem (Join-Path $taskRoot 'game') -Recurse -Filter openttd.exe)
if ($taskExecutables.Count -ne 1) { throw 'Expected one portable OpenTTD executable' }
$taskGame = $taskExecutables[0].Directory.FullName
Copy-Item (Join-Path $taskRoot 'graphics/*') (Join-Path $taskGame 'baseset') -Recurse -Force
"GUARD_TEST_GAME_DIR=$taskGame" | Out-File -LiteralPath $env:GITHUB_ENV -Encoding utf8 -Append
