# Package Windows build for GitHub Release (v1.0.0)
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repoRoot "My project\Output"
$dist = Join-Path $repoRoot "dist"
$zipName = "Library-Digital-Human-v1.0.0-windows.zip"
$zipPath = Join-Path $dist $zipName
$staging = Join-Path $dist "staging-v1.0.0"

if (-not (Test-Path (Join-Path $src "My project.exe"))) {
    Write-Host "ERROR: Build not found. Expected: $src\My project.exe"
    Write-Host "Build in Unity: File -> Build Settings -> Build to My project/Output"
    exit 1
}

if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
New-Item -ItemType Directory -Force -Path $dist | Out-Null
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Write-Host "Copying build files..."
Get-ChildItem $src | Where-Object { $_.Name -notlike "*_BurstDebugInformation_DoNotShip" } | ForEach-Object {
    Copy-Item $_.FullName -Destination $staging -Recurse -Force
}

Write-Host "Creating zip (may take a minute)..."
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -CompressionLevel Optimal

Remove-Item -Recurse -Force $staging

$mb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host ""
Write-Host "Done: $zipPath ($mb MB)"
Write-Host ""
Write-Host "Next: GitHub -> Releases -> Create a new release"
Write-Host "  Tag: v1.0.0"
Write-Host "  Upload: $zipName"
Write-Host "  Notes: copy from RELEASE_v1.0.0.md"
