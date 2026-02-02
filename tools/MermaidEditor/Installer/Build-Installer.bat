@echo off
REM Build script for Mermaid Editor Installer
REM This script publishes the application for Inno Setup

echo =====================================
echo Mermaid Editor Installer Build Script
echo =====================================
echo.

cd /d "%~dp0.."

echo Step 1: Publishing application...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

if %ERRORLEVEL% neq 0 (
    echo ERROR: dotnet publish failed!
    pause
    exit /b 1
)

echo.
echo =====================================
echo Publish complete!
echo =====================================
echo.
echo Published files are in: bin\Release\net10.0-windows\publish\win-x64
echo.
echo Next steps:
echo   1. Install Inno Setup 6 from: https://jrsoftware.org/isdl.php
echo   2. Open Installer\MermaidEditorSetup.iss in Inno Setup
echo   3. Click Build ^> Compile (or press Ctrl+F9)
echo   4. The installer will be created in bin\Installer\
echo.
pause
