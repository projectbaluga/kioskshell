@echo off
REM Needs the .NET 10 SDK (winget install Microsoft.DotNet.SDK.10, or download from dot.net).
dotnet publish KioskShell.csproj -c Release -o portable\KioskShell-Portable
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo.
echo Done. Portable folder: portable\KioskShell-Portable
