# Build script for Mermaid Editor Installer
# This script publishes the application and prepares it for Inno Setup

param(
    [switch]$SkipPublish,
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot "bin\Release\net10.0-windows\publish\win-x64"
$InstallerOutputDir = Join-Path $ProjectRoot "bin\Installer"

Write-Host "Mermaid Editor Installer Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Publish the application
if (-not $SkipPublish) {
    Write-Host "Step 1: Publishing application..." -ForegroundColor Yellow
    
    Push-Location $ProjectRoot
    try {
        # Publish as self-contained for Windows x64
        dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true
        
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "  Published to: $PublishDir" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "Step 1: Skipping publish (using existing files)" -ForegroundColor Yellow
}

# Step 2: Verify publish output exists
Write-Host ""
Write-Host "Step 2: Verifying publish output..." -ForegroundColor Yellow

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory not found: $PublishDir"
}

$exePath = Join-Path $PublishDir "MermaidEditor.exe"
if (-not (Test-Path $exePath)) {
    throw "MermaidEditor.exe not found in publish directory"
}

$fileCount = (Get-ChildItem $PublishDir -Recurse -File).Count
Write-Host "  Found $fileCount files in publish directory" -ForegroundColor Green

# Step 3: Create installer output directory
Write-Host ""
Write-Host "Step 3: Preparing installer output directory..." -ForegroundColor Yellow

if (-not (Test-Path $InstallerOutputDir)) {
    New-Item -ItemType Directory -Path $InstallerOutputDir -Force | Out-Null
}
Write-Host "  Installer output: $InstallerOutputDir" -ForegroundColor Green

# Step 4: Build installer (if Inno Setup is available)
if ($BuildInstaller) {
    Write-Host ""
    Write-Host "Step 4: Building installer with Inno Setup..." -ForegroundColor Yellow
    
    $innoSetupPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $innoSetupPath)) {
        $innoSetupPath = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    }
    
    if (Test-Path $innoSetupPath) {
        $issFile = Join-Path $PSScriptRoot "MermaidEditorSetup.iss"
        & $innoSetupPath $issFile
        
        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "  Installer created successfully!" -ForegroundColor Green
    }
    else {
        Write-Host "  Inno Setup not found. Please install from: https://jrsoftware.org/isdl.php" -ForegroundColor Red
        Write-Host "  After installing, run this script again with -BuildInstaller" -ForegroundColor Yellow
    }
}
else {
    Write-Host ""
    Write-Host "Step 4: Skipping installer build" -ForegroundColor Yellow
    Write-Host "  To build the installer, run: .\Build-Installer.ps1 -BuildInstaller" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Install Inno Setup 6 from: https://jrsoftware.org/isdl.php" -ForegroundColor White
Write-Host "  2. Run: .\Build-Installer.ps1 -BuildInstaller" -ForegroundColor White
Write-Host "  Or open MermaidEditorSetup.iss in Inno Setup and compile manually" -ForegroundColor White
