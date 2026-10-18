#Requires -RunAsAdministrator
<#
    Sets up IIS to host WT.Blazor with Windows Authentication.
    Run this from an elevated PowerShell after `dotnet publish -c Release -o publish`.
#>
param(
    [string]$SiteName = "WT.Blazor",
    [string]$AppPoolName = "WT.BlazorPool",
    [string]$PhysicalPath = "C:\inetpub\wwwroot\WT.Blazor",
    [string]$SourcePath = "$PSScriptRoot\..\WT.Blazor-publish",
    [int]$Port = 8081,
    [string]$ViewerGroup = "$env:COMPUTERNAME\WT-Viewers",
    [Alias("ContributerGroup")]
    [string]$ContributorGroup = "$env:COMPUTERNAME\WT-Contributors",
    [string]$AdminGroup = "$env:COMPUTERNAME\WT-Admins",
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [string]$CertificateThumbprint,
    [string]$HostName,
    [string]$AllowedHosts,
    [string]$SuperAdminGroup,
    [int]$HttpsPort = 443,
    [switch]$AllowInsecureHttp
)

Import-Module WebAdministration

function Assert-ValidGroup([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -match "^(YOUR|REPLACE|DOMAIN\\YOUR)[-_]" ) {
        throw "$Name must be a real Windows security group name, not an empty or placeholder value."
    }
}

function Sync-PublishedApplication {
    & robocopy $SourcePath $PhysicalPath /MIR /XD "App_Data" /R:5 /W:2 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -gt 7) {
        throw "Robocopy failed while synchronizing $SourcePath to $PhysicalPath (exit code $LASTEXITCODE)."
    }
    Write-Host "Release files synchronized; App_Data was preserved."
}

if (-not (Test-Path $SourcePath)) {
    throw "Published application directory not found: $SourcePath"
}
Assert-ValidGroup "ViewerGroup" $ViewerGroup
Assert-ValidGroup "ContributorGroup" $ContributorGroup
Assert-ValidGroup "AdminGroup" $AdminGroup
if ($PSBoundParameters.ContainsKey("SuperAdminGroup")) {
    throw "SuperAdminGroup is no longer a deployment parameter. Use -AdminGroup for administrators, including super-administrator access."
}
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint) -and [string]::IsNullOrWhiteSpace($HostName)) {
    throw "HostName must be supplied when CertificateThumbprint is used to configure an HTTPS binding."
}
if ([string]::IsNullOrWhiteSpace($HostName) -and -not $AllowInsecureHttp) {
    throw "HostName is required for HTTPS deployment. Use -AllowInsecureHttp only for local testing."
}
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $CertificateThumbprint = $CertificateThumbprint.Replace(" ", "")
    if (-not (Test-Path "Cert:\LocalMachine\My\$CertificateThumbprint")) {
        throw "Certificate $CertificateThumbprint was not found in Cert:\LocalMachine\My."
    }
}
if ([string]::IsNullOrWhiteSpace($AllowedHosts)) {
    $AllowedHosts = if ([string]::IsNullOrWhiteSpace($HostName)) { "localhost;$env:COMPUTERNAME;bttest.heilsunet.fo;bt.heilsunet.fo;btdev.heilsunet.fo" } else { "localhost;$env:COMPUTERNAME;bttest.heilsunet.fo;bt.heilsunet.fo;btdev.heilsunet.fo;$HostName" }
}
if ($AllowedHosts -eq "*") {
    throw "AllowedHosts must name the permitted hosts; wildcard values are not allowed."
}
# 1. Ensure the required Windows features are present (Windows Auth + ASP.NET Core hosting + WebSockets).
#    IIS-WebSockets is required for Blazor Server's SignalR circuit; without it, the browser falls
#    back to long polling, which can leave the interactive UI blank if it can't sustain the connection.
$features = @("IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", "IIS-WindowsAuthentication", "IIS-WebSockets")
$featureChanged = $false
foreach ($feature in $features) {
    $state = (Get-WindowsOptionalFeature -Online -FeatureName $feature).State
    if ($state -ne "Enabled") {
        Write-Host "Enabling $feature..."
        $result = Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
        if ((Get-WindowsOptionalFeature -Online -FeatureName $feature).State -ne "Enabled") {
            Write-Warning "$feature did not enable (no local source / blocked Windows Update?). Re-run with -Source pointing at a Windows installation image (install.wim/sources\sxs) if this keeps failing."
        } else {
            $featureChanged = $true
        }
    }
}

if ($featureChanged) {
    Write-Host "Restarting IIS so newly enabled features take effect..."
    iisreset | Out-Null
}

# 2. Install the ASP.NET Core Hosting Bundle if the IIS module (ANCM) is missing.
$ancmPath = "${env:ProgramFiles}\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
if (-not (Test-Path $ancmPath)) {
    Write-Host "Installing ASP.NET Core Hosting Bundle..."
    $installer = Join-Path $env:TEMP "dotnet-hosting-win.exe"
    Invoke-WebRequest -Uri "https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe" -OutFile $installer -UseBasicParsing
    $fileInfo = Get-Item $installer
    Write-Host "Downloaded installer: $($fileInfo.Length) bytes"
    if ($fileInfo.Length -lt 1MB) {
        throw "Downloaded file is too small ($($fileInfo.Length) bytes) to be the real installer - likely a proxy/redirect page, not the .exe. Open $installer in a text editor to check, or download manually from https://dotnet.microsoft.com/download/dotnet/10.0 (ASP.NET Core Runtime -> Hosting Bundle)."
    }

    $proc = Start-Process -FilePath $installer -ArgumentList "/quiet", "/norestart", "/log", "$env:TEMP\hosting-bundle-install.log" -Wait -PassThru
    Write-Host "Installer exit code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        throw "Hosting Bundle installer failed (exit code $($proc.ExitCode)). See $env:TEMP\hosting-bundle-install.log for details."
    }
    Remove-Item $installer -ErrorAction SilentlyContinue
    Write-Host "Restarting IIS so it picks up the new module..."
    iisreset | Out-Null
}

if (-not (Test-Path $ancmPath)) {
    throw "ASP.NET Core Module still not found at $ancmPath after install attempt. Install the Hosting Bundle manually from https://dotnet.microsoft.com/download/dotnet/10.0 and re-run this script."
}

# 3. Create a dedicated app pool (No Managed Code - ASP.NET Core doesn't use the CLR host).
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode -Value "AlwaysRunning"
# Blazor Server relies on a long-lived SignalR circuit (WebSocket or long-polling). IIS's default
# 20-minute idle timeout and periodic recycling both kill that circuit out from under active users,
# leaving a blank page until they reload. Disable both, per Microsoft's Blazor Server + IIS guidance.
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)

# 4. Create or update the site before verifying a manually managed HTTPS binding.
if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName -Port $Port | Out-Null
}
else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    $defaultHttpBinding = Get-WebBinding -Name $SiteName -Protocol "http" |
        Where-Object { $_.bindingInformation -match "^\*:\d+:$" } |
        Select-Object -First 1
    if ($defaultHttpBinding -and $defaultHttpBinding.bindingInformation -ne "*:${Port}:") {
        Remove-WebBinding -Name $SiteName -Protocol "http" -BindingInformation $defaultHttpBinding.bindingInformation
        New-WebBinding -Name $SiteName -Protocol "http" -Port $Port | Out-Null
    }
    elseif (-not $defaultHttpBinding) {
        New-WebBinding -Name $SiteName -Protocol "http" -Port $Port | Out-Null
    }
}

$httpsCertificateNeedsManualSelection = $false
if ([string]::IsNullOrWhiteSpace($CertificateThumbprint) -and -not [string]::IsNullOrWhiteSpace($HostName)) {
    $httpsBindingInformation = "*:${HttpsPort}:$HostName"
    $existingHttpsBinding = Get-WebBinding -Name $SiteName -Protocol "https" -ErrorAction SilentlyContinue |
        Where-Object { $_.bindingInformation -eq $httpsBindingInformation } |
        Select-Object -First 1
    if (-not $existingHttpsBinding) {
        New-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -HostHeader $HostName -SslFlags 1 | Out-Null
        $httpsCertificateNeedsManualSelection = $true
        Write-Warning "Created the HTTPS binding for $HostName. Select its certificate in IIS Manager after deployment."
    }
}

# 5. Back up the current database, then synchronize release files to the IIS folder.
#    Stop the app pool and wait for its worker process to fully exit so file locks are released.
$databasePath = Join-Path $PhysicalPath "App_Data\wt-blazor.db"
if (Test-Path $databasePath) {
    $backupFile = & (Join-Path $PSScriptRoot "Backup-Database.ps1") -AppPoolName $AppPoolName -DatabasePath $databasePath -BackupPath $BackupPath
    if (-not $backupFile -or -not (Test-Path $backupFile)) {
        throw "Database backup did not produce a file. Deployment was not started."
    }
    Write-Host "Database backup created: $backupFile"
}
else {
    Write-Host "No existing database found; skipping pre-deployment backup."
}

if (Test-Path "IIS:\AppPools\$AppPoolName") {
    Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue

    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline -and (Get-WebAppPoolState -Name $AppPoolName).Value -ne "Stopped") {
        Start-Sleep -Milliseconds 500
    }

    Get-CimInstance Win32_Process -Filter "Name = 'w3wp.exe'" |
        Where-Object { $_.CommandLine -like "*$AppPoolName*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    Start-Sleep -Seconds 1
}

New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
Sync-PublishedApplication

New-Item -ItemType Directory -Path (Join-Path $PhysicalPath "App_Data") -Force | Out-Null

# 2b. The copy above just overwrote appsettings.json with whatever was baked into the publish
#     output. Patch in the real Windows security group names every time so this never silently regresses.
$settingsPath = Join-Path $PhysicalPath "appsettings.json"
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
if (-not $settings.AdGroups) {
    $settings | Add-Member -NotePropertyName AdGroups -NotePropertyValue ([pscustomobject]@{}) -Force
}
if (-not $settings.Hosting) {
    $settings | Add-Member -NotePropertyName Hosting -NotePropertyValue ([pscustomobject]@{}) -Force
}
if (-not $settings.Backup) {
    $settings | Add-Member -NotePropertyName Backup -NotePropertyValue ([pscustomobject]@{}) -Force
}
$settings.AdGroups | Add-Member -NotePropertyName Viewer -NotePropertyValue $ViewerGroup -Force
$settings.AdGroups | Add-Member -NotePropertyName Contributor -NotePropertyValue $ContributorGroup -Force
$settings.AdGroups | Add-Member -NotePropertyName Admin -NotePropertyValue $AdminGroup -Force
$settings.Backup | Add-Member -NotePropertyName Path -NotePropertyValue $BackupPath -Force
$redirectToHttps = -not $AllowInsecureHttp
$settings.Hosting | Add-Member -NotePropertyName RedirectToHttps -NotePropertyValue $redirectToHttps -Force
$settings | Add-Member -NotePropertyName AllowedHosts -NotePropertyValue $AllowedHosts -Force
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath
Write-Host "HTTPS redirect is $redirectToHttps; allowed hosts are $AllowedHosts."

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $httpsBindingInformation = "*:${HttpsPort}:$HostName"
    $existingHttpsBinding = Get-WebBinding -Name $SiteName -Protocol "https" |
        Where-Object { $_.bindingInformation -eq $httpsBindingInformation } |
        Select-Object -First 1
    if (-not $existingHttpsBinding) {
        New-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -HostHeader $HostName -SslFlags 1 | Out-Null
        $existingHttpsBinding = Get-WebBinding -Name $SiteName -Protocol "https" |
            Where-Object { $_.bindingInformation -eq $httpsBindingInformation } |
            Select-Object -First 1
    }
    $existingHttpsBinding.AddSslCertificate($CertificateThumbprint, "My")
}

# 5. Enable Windows + Anonymous auth (app-level [Authorize] policies decide what actually requires AD).
#    Unlock the sections first - Server-level config sometimes has them locked/absent until the
#    Windows Authentication role feature has fully registered its schema.
Set-WebConfiguration -Filter "/system.webServer/security/authentication/windowsAuthentication" `
    -PSPath "MACHINE/WEBROOT/APPHOST" -Metadata overrideMode -Value Allow
Set-WebConfiguration -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
    -PSPath "MACHINE/WEBROOT/APPHOST" -Metadata overrideMode -Value Allow

try {
    Set-WebConfigurationProperty -Filter "/system.webServer/security/authentication/windowsAuthentication" `
        -PSPath "IIS:\Sites\$SiteName" -Name Enabled -Value True
    Set-WebConfigurationProperty -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
        -PSPath "IIS:\Sites\$SiteName" -Name Enabled -Value True
}
catch {
    throw "Could not enable Windows/Anonymous authentication - the IIS-WindowsAuthentication feature likely isn't fully installed. Check with: Get-WindowsOptionalFeature -Online -FeatureName IIS-WindowsAuthentication. Original error: $_"
}

# 6. Let the app pool identity read/write the SQLite db under App_Data.
$acl = Get-Acl $PhysicalPath
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\$AppPoolName", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($rule)
Set-Acl $PhysicalPath $acl

# 7. Start the app pool back up (it was stopped earlier to release file locks for the copy).
Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
if ([string]::IsNullOrWhiteSpace($HostName)) {
    & (Join-Path $PSScriptRoot "Test-Deployment.ps1") -Port $Port -ViewerGroup $ViewerGroup -ContributorGroup $ContributorGroup -AdminGroup $AdminGroup
}
elseif ($httpsCertificateNeedsManualSelection) {
    Write-Warning "Deployment completed. Select the certificate for https://$HostName in IIS Manager, then verify the site in a browser."
}
else {
    & (Join-Path $PSScriptRoot "Test-Deployment.ps1") -Scheme "https" -HostName $HostName -Port $HttpsPort -ViewerGroup $ViewerGroup -ContributorGroup $ContributorGroup -AdminGroup $AdminGroup
}

if ([string]::IsNullOrWhiteSpace($HostName)) {
    Write-Host "Done. Browse to http://localhost:$Port/ (or http://HNPV-WAC05:$Port/) to test."
}
else {
    Write-Host "Done. Browse to https://${HostName}:$HttpsPort/ to test."
}
Write-Host "AdGroups.Viewer=$ViewerGroup, AdGroups.Contributor=$ContributorGroup, AdGroups.Admin=$AdminGroup (pass -ViewerGroup/-ContributorGroup/-AdminGroup/-AllowedHosts to change)."
