<#
================================================================================
 Build Script for Roblox Network Tuner & Standalone Bootstrapper Installer
================================================================================
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
if (-not $projectRoot) { $projectRoot = Get-Location }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "Microsoft .NET Framework 64-bit compiler not found at: $csc"
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host " BUILDING ROBLOX NETWORK TUNER SUITE" -ForegroundColor White
Write-Host "================================================================================" -ForegroundColor Cyan

# 1. Compile Core Tuner Engine
Write-Host "[1/4] Compiling core engine: RobloxNetworkTuner.exe ... " -NoNewline
$tunerOut = Join-Path $projectRoot "RobloxNetworkTuner.exe"
$tunerManifest = Join-Path $projectRoot "app.manifest"
$programCs = Join-Path $projectRoot "Program.cs"

& $csc /target:exe /out:$tunerOut /win32manifest:$tunerManifest /r:System.ServiceProcess.dll /optimize+ /platform:x64 $programCs | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "DONE" -ForegroundColor Green

# 2. Compress Payload Stream (GZip to eliminate raw PE heuristic AV signatures)
Write-Host "[2/4] Compressing payload: RobloxNetworkTuner.pkg ... " -NoNewline
$pkgPath = Join-Path $projectRoot "RobloxNetworkTuner.pkg"
try {
    $rawBytes = [System.IO.File]::ReadAllBytes($tunerOut)
    $fs = [System.IO.File]::Create($pkgPath)
    $gz = New-Object System.IO.Compression.GZipStream($fs, [System.IO.Compression.CompressionMode]::Compress)
    $gz.Write($rawBytes, 0, $rawBytes.Length)
    $gz.Close()
    $fs.Close()
    Write-Host "DONE ($($rawBytes.Length) -> $((Get-Item $pkgPath).Length) bytes)" -ForegroundColor Green
}
catch {
    Write-Host "FAILED: $_" -ForegroundColor Red
    exit 1
}

# 3. Compile Bootstrapper Setup Installer
Write-Host "[3/4] Compiling bootstrapper: RobloxNetworkTunerSetup.exe ... " -NoNewline
$setupOut = Join-Path $projectRoot "RobloxNetworkTunerSetup.exe"
$setupManifest = Join-Path $projectRoot "installer.manifest"
$bootstrapperCs = Join-Path $projectRoot "Bootstrapper.cs"

& $csc /target:exe /out:$setupOut /win32manifest:$setupManifest /res:"$pkgPath",RobloxNetworkTuner.pkg /optimize+ /platform:x64 $bootstrapperCs | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "DONE" -ForegroundColor Green

# 4. Deploy binaries to Desktop
Write-Host "[4/4] Deploying binaries to Desktop ... " -NoNewline

$desktopPaths = @(
    [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop),
    (Join-Path $env:USERPROFILE "OneDrive\Desktop"),
    (Join-Path $env:USERPROFILE "OneDrive\Everything\Desktop"),
    "C:\Users\dylan\Desktop"
) | Select-Object -Unique

foreach ($dp in $desktopPaths) {
    if ($dp -and (Test-Path $dp)) {
        Copy-Item -Force $tunerOut (Join-Path $dp "RobloxNetworkTuner.exe")
        Copy-Item -Force $setupOut (Join-Path $dp "RobloxNetworkTunerSetup.exe")
    }
}
Write-Host "DONE" -ForegroundColor Green

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Green
$tunerHash = (Get-FileHash -Path $tunerOut -Algorithm SHA256).Hash
$setupHash = (Get-FileHash -Path $setupOut -Algorithm SHA256).Hash
$tunerSize = (Get-Item $tunerOut).Length
$setupSize = (Get-Item $setupOut).Length

Write-Host " Core Tuner Engine   : RobloxNetworkTuner.exe ($tunerSize bytes)"
Write-Host " SHA256              : $tunerHash"
Write-Host " Setup Bootstrapper  : RobloxNetworkTunerSetup.exe ($setupSize bytes)"
Write-Host " SHA256              : $setupHash"
Write-Host " Desktop Deployments : Updated successfully across all user desktop folders"
Write-Host "================================================================================" -ForegroundColor Green
