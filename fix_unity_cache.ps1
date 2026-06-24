# Close Unity Editor before running.
$repoRoot = $PSScriptRoot
$library = Join-Path $repoRoot "My project\Library"
$disabledPrefabs = Join-Path $repoRoot "_disabled_unity_assets\Characters"

if (Get-Process -Name "Unity" -ErrorAction SilentlyContinue) {
    Write-Host "ERROR: Close Unity Editor first, then run again."
    exit 1
}

Write-Host "Repo: $repoRoot"
Write-Host "Library: $library"
Write-Host ""

if (Test-Path $library) {
    Write-Host "Removing Library cache..."
    Remove-Item -Recurse -Force $library
    Write-Host "Done. Reopen Unity (first import may take a while)."
} else {
    Write-Host "Library not found (already cleaned or wrong path)."
}

Write-Host ""
if (Test-Path $disabledPrefabs) {
    Write-Host "Disabled prefabs: $disabledPrefabs"
}
