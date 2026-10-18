#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$ViewerGroup = "$env:COMPUTERNAME\WT-Viewers",
    [string]$ContributorGroup = "$env:COMPUTERNAME\WT-Contributors",
    [string]$AdminGroup = "$env:COMPUTERNAME\WT-Admins",
    [string]$SourcePath = "$PSScriptRoot\..\..\WT.Blazor-publish",
    [string]$SiteName = "WT.Blazor",
    [string]$AppPoolName = "WT.BlazorPool",
    [string]$PhysicalPath = "C:\inetpub\wwwroot\WT.Blazor",
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [int]$Port = 8081,
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
    -HostName "btdev.heilsunet.fo" `
    -AllowedHosts "localhost;$env:COMPUTERNAME;btdev.heilsunet.fo"