#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$ViewerGroup = "$env:COMPUTERNAME\WT-Viewers",
    [string]$ContributorGroup = "$env:COMPUTERNAME\WT-Contributors",
    [string]$AdminGroup = "$env:COMPUTERNAME\WT-Admins",
    [string]$SourcePath = "$PSScriptRoot\..\WT.Blazor-publish",
    [string]$SiteName = "WT",
    [string]$AppPoolName = "WTPool",
    [string]$PhysicalPath = "C:\inetpub\wwwroot\WT",
    [string]$BackupPath = "C:\inetpub\backups\WT",
    [int]$Port = 8082,
    [string]$CertificateThumbprint
)

& (Join-Path $PSScriptRoot "Setup-Iis.ps1") `
    -SiteName $SiteName `
    -AppPoolName $AppPoolName `
    -PhysicalPath $PhysicalPath `
    -BackupPath $BackupPath `
    -Port $Port `
    -CertificateThumbprint $CertificateThumbprint `
    -ViewerGroup $ViewerGroup `
    -ContributorGroup $ContributorGroup `
    -AdminGroup $AdminGroup `
    -SourcePath $SourcePath `
    -HostName "bttest.heilsunet.fo" `
    -AllowedHosts "localhost;$env:COMPUTERNAME;bttest.heilsunet.fo"