#requires -RunAsAdministrator
[CmdletBinding()]
param(
  [Parameter(Mandatory)][ValidateSet('Install','Remove')][string]$Action,
  [string]$InstallDir = 'C:\KioskShell2',
  [string]$DisplayName = 'KioskShell'
)
$ErrorActionPreference = 'Stop'
$stateDir = Join-Path $env:ProgramData 'KioskShell2'
$stateFile = Join-Path $stateDir 'install-state.json'
New-Item -ItemType Directory -Force -Path $stateDir | Out-Null

function Set-AssignedAccess([string]$XmlPath) {
  $runner = Join-Path $stateDir 'apply-assigned-access.ps1'
  $body = @'
$ErrorActionPreference = 'Stop'
try {
  $obj = Get-CimInstance -Namespace 'root\cimv2\mdm\dmmap' -ClassName 'MDM_AssignedAccess'
  $xmlPath = Join-Path $env:ProgramData 'KioskShell2\assigned-access.xml'
  if (Test-Path $xmlPath) {
    $obj.Configuration = [System.Net.WebUtility]::HtmlEncode((Get-Content -LiteralPath $xmlPath -Raw))
  } else {
    $obj.Configuration = $null
  }
  Set-CimInstance -CimInstance $obj | Out-Null
  'OK' | Set-Content -LiteralPath (Join-Path $env:ProgramData 'KioskShell2\result.txt')
} catch {
  ('FAILED: ' + $_.Exception.Message) | Set-Content -LiteralPath (Join-Path $env:ProgramData 'KioskShell2\result.txt')
}
'@
  Set-Content -LiteralPath $runner -Value $body -Encoding UTF8
  $resultPath = Join-Path $stateDir 'result.txt'
  Remove-Item $resultPath -Force -ErrorAction SilentlyContinue
  $task = 'KioskShell2-AssignedAccess'
  $taskAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$runner`""
  $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
  Register-ScheduledTask -TaskName $task -Action $taskAction -Principal $principal -Force | Out-Null
  try {
    Start-ScheduledTask -TaskName $task
    $deadline = (Get-Date).AddSeconds(60)
    while (!(Test-Path $resultPath) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    if (!(Test-Path $resultPath)) { throw 'Timed out applying Assigned Access.' }
    $result = (Get-Content $resultPath -Raw).Trim()
    if ($result -ne 'OK') { throw $result }
  } finally {
    Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
  }
}

if ($Action -eq 'Install') {
  $sourceExe = Join-Path $PSScriptRoot 'KioskShell2.exe'
  if (!(Test-Path $sourceExe)) { throw 'KioskShell2.exe must be beside this installer.' }
  $edgePaths = @(
    (Join-Path $env:ProgramFiles(x86) 'Microsoft\Edge\Application\msedge.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe')
  )
  if (-not ($edgePaths | Where-Object { Test-Path $_ })) { throw 'Microsoft Edge Stable is required.' }
  New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot '*') -Destination $InstallDir -Recurse -Force
  $exe = Join-Path $InstallDir 'KioskShell2.exe'
  if (!(Test-Path $exe)) { throw 'Copying KioskShell2 failed.' }

  $obj = Get-CimInstance -Namespace 'root\cimv2\mdm\dmmap' -ClassName 'MDM_AssignedAccess'
  [ordered]@{ PreviousConfiguration = $obj.Configuration; InstallDir = $InstallDir; InstalledAt = (Get-Date).ToString('o') } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding UTF8

  $id = '{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}'
  $exeXml = [System.Security.SecurityElement]::Escape($exe)
  $nameXml = [System.Security.SecurityElement]::Escape($DisplayName)
  $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<AssignedAccessConfiguration xmlns="http://schemas.microsoft.com/AssignedAccess/2017/config"
 xmlns:rs5="http://schemas.microsoft.com/AssignedAccess/201810/config"
 xmlns:v4="http://schemas.microsoft.com/AssignedAccess/2021/config">
 <Profiles><Profile Id="$id"><KioskModeApp v4:ClassicAppPath="$exeXml" /></Profile></Profiles>
 <Configs><Config><AutoLogonAccount rs5:DisplayName="$nameXml" /><DefaultProfile Id="$id" /></Config></Configs>
</AssignedAccessConfiguration>
"@
  $xmlPath = Join-Path $stateDir 'assigned-access.xml'
  Set-Content -LiteralPath $xmlPath -Value $xml -Encoding UTF8
  try {
    Set-AssignedAccess $xmlPath
    Set-Content -LiteralPath (Join-Path $InstallDir '.kioskshell2-installed') -Value '2.0' -Encoding ASCII
    Write-Host "KioskShell 2.0 installed to $InstallDir. Restart Windows to enter kiosk mode." -ForegroundColor Green
  } catch {
    $previous = (Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json).PreviousConfiguration
    if ($previous) { Set-Content -LiteralPath $xmlPath -Value $previous -Encoding UTF8; Set-AssignedAccess $xmlPath }
    throw
  }
}
else {
  if (!(Test-Path $stateFile)) { throw 'Install state is missing; refusing to overwrite unknown Assigned Access configuration.' }
  $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
  $xmlPath = Join-Path $stateDir 'assigned-access.xml'
  if ([string]::IsNullOrWhiteSpace([string]$state.PreviousConfiguration)) {
    Remove-Item $xmlPath -Force -ErrorAction SilentlyContinue
  } else {
    Set-Content -LiteralPath $xmlPath -Value ([string]$state.PreviousConfiguration) -Encoding UTF8
  }
  Set-AssignedAccess $xmlPath
  Remove-Item (Join-Path $state.InstallDir '.kioskshell2-installed') -Force -ErrorAction SilentlyContinue
  Write-Host 'Previous Assigned Access configuration restored. Installed files were retained.' -ForegroundColor Green
}
