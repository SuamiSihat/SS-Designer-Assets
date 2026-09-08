# build-linux-package.ps1
# Builds the self-contained native Linux desktop package (SS-CAM.Linux) and packages into tar.gz

[CmdletBinding()]
param(
    [string]$Version = "4.6.2"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$linuxProj = Join-Path $repoRoot 'src\SS-CAM.Linux\SS-CAM.Linux.csproj'
$distDir   = Join-Path $repoRoot 'dist'
$tempOut   = Join-Path $repoRoot 'dist\linux-x64'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building SS-CAM Linux Native Desktop Package v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

if (Test-Path $tempOut) {
    Remove-Item -Path $tempOut -Recurse -Force | Out-Null
}
New-Item -ItemType Directory -Path $tempOut -Force | Out-Null

Write-Host "[1/3] Compiling self-contained native single-file Linux binary via dotnet..." -ForegroundColor Yellow
dotnet publish $linuxProj `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $tempOut

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed for Linux x64."
    exit 1
}

# Copy brand icon into package
$iconPath = Join-Path $repoRoot 'installer\assets\ss-cam.svg'
if (Test-Path $iconPath) {
    Copy-Item $iconPath (Join-Path $tempOut 'ss-cam.svg') -Force
}

Write-Host "[2/3] Packaging tar.gz archive..." -ForegroundColor Yellow
$tarNameVersion = "SS-CAM-v$Version-linux-x64.tar.gz"
$tarNameGeneric = "ss-cam-linux-x64.tar.gz"

$tarPathVersion = Join-Path $distDir $tarNameVersion
$tarPathGeneric = Join-Path $distDir $tarNameGeneric

if (Test-Path $tarPathVersion) { Remove-Item $tarPathVersion -Force }
if (Test-Path $tarPathGeneric) { Remove-Item $tarPathGeneric -Force }

# Use tar if available (standard in Windows 10/11)
if (Get-Command tar -ErrorAction SilentlyContinue) {
    Push-Location $tempOut
    try {
        tar -czf $tarPathVersion *
        Copy-Item $tarPathVersion $tarPathGeneric -Force
    } finally {
        Pop-Location
    }
} else {
    Write-Warning "tar command not found on Windows. Linux binary published to dist/linux-x64/"
}

Write-Host "[3/3] Package complete!" -ForegroundColor Green
if (Test-Path $tarPathVersion) {
    $sizeMb = [Math]::Round((Get-Item $tarPathVersion).Length / 1MB, 2)
    Write-Host "  -> $tarPathVersion ($sizeMb MB)" -ForegroundColor Green
    Write-Host "  -> $tarPathGeneric ($sizeMb MB)" -ForegroundColor Green
}
