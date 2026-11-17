@echo off
REM Needs the .NET 10 SDK (winget install Microsoft.DotNet.SDK.10, or download from dot.net).

echo Publishing single-file portable build...
dotnet publish KioskShell.csproj -c Release -o portable\KioskShell-Portable ^
  -p:PublishSingleFile=true ^
  -p:SelfContained=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (echo BUILD FAILED & exit /b 1)

echo.
echo Cleaning extra files...
REM Remove debug / satellite noise
del /q portable\KioskShell-Portable\*.pdb 2>nul
del /q portable\KioskShell-Portable\*.xml 2>nul
del /q portable\KioskShell-Portable\*.deps.json 2>nul
del /q portable\KioskShell-Portable\Microsoft.Web.WebView2.Wpf* 2>nul

echo.
echo Done. Portable folder: portable\KioskShell-Portable
echo Expected core files: KioskShell.exe + kiosk.json + assets\ + WebView2Loader.dll (if required)
