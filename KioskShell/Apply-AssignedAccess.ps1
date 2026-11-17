# Run as SYSTEM, e.g.:  PsExec.exe -i -s powershell.exe   then:  .\Apply-AssignedAccess.ps1
# (Alternatively apply AssignedAccess.xml through Intune or a provisioning package.)
$ns  = "root\cimv2\mdm\dmmap"
$cls = "MDM_AssignedAccess"
$xml = Get-Content -Raw -Path "$PSScriptRoot\AssignedAccess.xml"
$obj = Get-CimInstance -Namespace $ns -ClassName $cls
$obj.Configuration = [System.Net.WebUtility]::HtmlEncode($xml)
Set-CimInstance -CimInstance $obj
Write-Host "Applied. Sign out, then sign in as the kiosk account to test."
# To remove later: Clear-AssignedAccess
