# Run after closing Unity Editor. Clears broken Library cache.
$projectRoot = Split-Path -Parent $PSScriptRoot
$library = Join-Path $projectRoot "My project\Library"

if (Get-Process -Name "Unity" -ErrorAction SilentlyContinue) {
    Write-Host "Close Unity Editor first, then run this script again." -ForegroundColor Red
    exit 1
}

if (Test-Path $library) {
    Write-Host "Removing: $library"
    Remove-Item -Recurse -Force $library
    Write-Host "Library removed. Reopen Unity to reimport (may take a while)." -ForegroundColor Green
} else {
    Write-Host "Library folder not found, nothing to clean."
}

Write-Host ""
Write-Host "Disabled prefabs: $projectRoot\_disabled_unity_assets\Characters"
