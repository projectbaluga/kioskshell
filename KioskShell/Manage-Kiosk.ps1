<#
.SYNOPSIS
  Installs or removes the KioskShell kiosk (Windows 11 Pro / Enterprise / Education).
  Run from an elevated (Administrator) PowerShell, in the portable KioskShell folder.

.EXAMPLE
  .\Manage-Kiosk.ps1 -Action Install
  .\Manage-Kiosk.ps1 -Action Install -InstallDir D:\Kiosk
  .\Manage-Kiosk.ps1 -Action Remove
#>
param(
    [Parameter(Mandatory)][ValidateSet('Install', 'Remove')][string]$Action,
    [string]$InstallDir = 'C:\KioskShell',
    [string]$DisplayName = 'Kiosk'
)

$ErrorActionPreference = 'Stop'
$here   = $PSScriptRoot
$workDir = Join-Path $env:ProgramData 'KioskShell'
$log    = Join-Path $workDir 'apply.log'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run this from an elevated (Administrator) PowerShell.' }

function Invoke-AsSystem([string]$Code) {
    # Assigned Access is configured through the MDM bridge, which only accepts SYSTEM.
    # We run the code once as SYSTEM through a temporary scheduled task.
    New-Item -ItemType Directory -Force -Path $workDir | Out-Null
    Remove-Item $log -ErrorAction SilentlyContinue
    $script = Join-Path $workDir 'run-as-system.ps1'

    $lines = @(
        '$ErrorActionPreference = ''Stop''',
        "`$logPath = '$log'",
        'try {',
        $Code,
        "  'OK' | Out-File -FilePath `$logPath -Encoding utf8",
        '} catch {',
        "  ('FAILED: ' + `$_.Exception.Message) | Out-File -FilePath `$logPath -Encoding utf8",
        '}'
    )
    Set-Content -Path $script -Value $lines -Encoding UTF8

    $name = 'KioskShell-Apply'
    $act  = New-ScheduledTaskAction -Execute 'powershell.exe' `
            -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`""
    $prin = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $name -Action $act -Principal $prin -Force | Out-Null
    Start-ScheduledTask -TaskName $name

    $waited = 0
    while (-not (Test-Path $log) -and $waited -lt 60) { Start-Sleep -Seconds 1; $waited++ }
    Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue

    $result = if (Test-Path $log) { (Get-Content $log -Raw).Trim() } else { 'FAILED: timed out' }
    if ($result -ne 'OK') { throw "Assigned Access change failed: $result" }
}

if ($Action -eq 'Install') {
    $exeName = 'KioskShell.exe'
    if (-not (Test-Path (Join-Path $here $exeName))) { throw "$exeName not found next to this script." }

    # 1. Copy the portable folder to its final home (skipped if it is already there).
    $src = (Resolve-Path $here).Path.TrimEnd('\')
    $dst = $InstallDir.TrimEnd('\')
    if ($src -ne $dst) {
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item -Path (Join-Path $src '*') -Destination $dst -Recurse -Force
    }
    $exe = Join-Path $dst $exeName

    # 2. Build the Assigned Access configuration: single-app kiosk running KioskShell.exe,
    #    with a managed auto-sign-in account (Windows creates it for you).
    $guid   = '{' + [guid]::NewGuid().ToString().ToUpper() + '}'
    $exeXml = [System.Security.SecurityElement]::Escape($exe)
    $xml = @"
<?xml version="1.0" encoding="utf-8" ?>
<AssignedAccessConfiguration
    xmlns="http://schemas.microsoft.com/AssignedAccess/2017/config"
    xmlns:rs5="http://schemas.microsoft.com/AssignedAccess/201810/config"
    xmlns:v4="http://schemas.microsoft.com/AssignedAccess/2021/config">
  <Profiles>
    <Profile Id="$guid">
      <KioskModeApp v4:ClassicAppPath="$exeXml" />
    </Profile>
  </Profiles>
  <Configs>
    <Config>
      <AutoLogonAccount rs5:DisplayName="$([System.Security.SecurityElement]::Escape($DisplayName))" />
      <DefaultProfile Id="$guid" />
    </Config>
  </Configs>
</AssignedAccessConfiguration>
"@
    $xmlPath = Join-Path $dst 'AssignedAccess.generated.xml'
    Set-Content -Path $xmlPath -Value $xml -Encoding UTF8

    # 3. Apply it (as SYSTEM).
    $code = @"
`$obj = Get-CimInstance -Namespace 'root\cimv2\mdm\dmmap' -ClassName 'MDM_AssignedAccess'
`$obj.Configuration = [System.Net.WebUtility]::HtmlEncode((Get-Content -Raw -Path '$xmlPath'))
Set-CimInstance -CimInstance `$obj
"@
    Invoke-AsSystem $code

    # 4. Apply the kiosk-specific Edge swipe policy while preserving the previous value for uninstall.
    $edgePath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\EdgeUI'
    $edgeBefore = Get-ItemProperty -Path $edgePath -Name AllowEdgeSwipe -ErrorAction SilentlyContinue
    $state = [ordered]@{ EdgeKeyExisted = Test-Path $edgePath; EdgeValueExisted = ($null -ne $edgeBefore); EdgeValue = if ($null -ne $edgeBefore) { [int]$edgeBefore.AllowEdgeSwipe } else { $null } }
    $state | ConvertTo-Json | Set-Content -Path (Join-Path $workDir 'install-state.json') -Encoding UTF8
    New-Item -Path $edgePath -Force | Out-Null
    New-ItemProperty -Path $edgePath -Name AllowEdgeSwipe -PropertyType DWord -Value 0 -Force | Out-Null
    Set-Content -Path (Join-Path $dst '.kioskshell-installed') -Value '1' -Encoding ASCII

    Write-Host ''
    Write-Host "Installed to: $dst" -ForegroundColor Green
    Write-Host 'Restart the PC. It will sign in to the kiosk account and start KioskShell.' -ForegroundColor Green

    # Smart App Control blocks unsigned apps. 1 = on (will block KioskShell.exe), 0 = off, 2 = evaluation.
    $sac = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' `
            -Name VerifiedAndReputablePolicyState -ErrorAction SilentlyContinue).VerifiedAndReputablePolicyState
    if ($sac -eq 1) {
        Write-Warning 'Smart App Control is ON and will block this unsigned app. Turn it off in Windows Security > App & browser control > Smart App Control settings, or sign KioskShell.exe.'
    }
    Write-Host 'To get out of the kiosk later: Ctrl+Alt+Del (or Windows button + Power on a tablet).'
}

if ($Action -eq 'Remove') {
    $code = @"
`$obj = Get-CimInstance -Namespace 'root\cimv2\mdm\dmmap' -ClassName 'MDM_AssignedAccess'
`$obj.Configuration = `$null
Set-CimInstance -CimInstance `$obj
"@
    Invoke-AsSystem $code
    $edgePath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\EdgeUI'
    $statePath = Join-Path $workDir 'install-state.json'
    if (Test-Path $statePath) {
        $state = Get-Content $statePath -Raw | ConvertFrom-Json
        if ($state.EdgeValueExisted) {
            New-Item -Path $edgePath -Force | Out-Null
            New-ItemProperty -Path $edgePath -Name AllowEdgeSwipe -PropertyType DWord -Value ([int]$state.EdgeValue) -Force | Out-Null
        } else {
            Remove-ItemProperty -Path $edgePath -Name AllowEdgeSwipe -ErrorAction SilentlyContinue
            if (-not $state.EdgeKeyExisted) { Remove-Item -Path $edgePath -ErrorAction SilentlyContinue }
        }
        Remove-Item $statePath -Force -ErrorAction SilentlyContinue
    }
    Remove-Item (Join-Path $InstallDir '.kioskshell-installed') -Force -ErrorAction SilentlyContinue

    Write-Host 'Kiosk configuration removed. Restart and sign in with your normal account.' -ForegroundColor Green
    Write-Host 'Files were left in place. The auto-created kiosk account can be deleted in Settings > Accounts if you no longer need it.'
}
