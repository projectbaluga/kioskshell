@echo off
REM Requires the .NET 10 SDK (winget install Microsoft.DotNet.SDK.10, or download from dot.net)
dotnet publish KioskShell.csproj -c Release -r win-x64 --self-contained true -o publish
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo.
echo Done. Output is in the "publish" folder. Edit publish\kiosk.json, then run publish\KioskShell.exe
