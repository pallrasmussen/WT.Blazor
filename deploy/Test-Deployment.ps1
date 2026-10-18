param(
    [int]$Port = 8081,
    [ValidateSet("http", "https")]
    [string]$Scheme = "http",
    [string]$HostName = "localhost",
    [int]$TimeoutSeconds = 30,
    [string]$ViewerGroup = "$env:COMPUTERNAME\WT-Viewers",
    [string]$ContributorGroup = "$env:COMPUTERNAME\WT-Contributors",
    [string]$AdminGroup = "$env:COMPUTERNAME\WT-Admins"
)

function Assert-LocalSecurityGroup([string]$Name, [string]$QualifiedGroupName) {
    $groupName = $QualifiedGroupName.Split('\')[-1]
    $group = Get-LocalGroup -Name $groupName -ErrorAction SilentlyContinue
    if ($null -eq $group) {
        throw "$Name local security group was not found: $QualifiedGroupName"
    }
}

function Show-StartupDiagnostics {
    Get-WinEvent -FilterHashtable @{ LogName = "Application"; StartTime = (Get-Date).AddMinutes(-5) } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -in "IIS AspNetCore Module V2", ".NET Runtime" } |
        Select-Object -First 10 TimeCreated, ProviderName, Id, LevelDisplayName, Message |
        Format-List | Out-String | Write-Host
}

function Get-EndpointStatus([string]$Uri) {
    try {
        return (Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop).StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

$baseUri = "${Scheme}://${HostName}:$Port"
Assert-LocalSecurityGroup "Viewer" $ViewerGroup
Assert-LocalSecurityGroup "Contributor" $ContributorGroup
Assert-LocalSecurityGroup "Admin" $AdminGroup
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$healthStatus = $null
do {
    try {
        $healthStatus = Get-EndpointStatus "$baseUri/health"
        if ($healthStatus -eq 200) { break }
    }
    catch {
        $healthStatus = $null
    }
    Start-Sleep -Seconds 1
} while ((Get-Date) -lt $deadline)

if ($healthStatus -ne 200) {
    Write-Host "Deployment health check failed. Recent IIS/.NET diagnostics:"
    Show-StartupDiagnostics
    throw "Expected GET $baseUri/health to return 200, but received $healthStatus."
}

if ((Get-EndpointStatus "$baseUri/") -ne 200) {
    throw "Expected GET $baseUri/ to return 200."
}

$internalStatus = Get-EndpointStatus "$baseUri/internal"
if ($internalStatus -notin 200, 401, 403) {
    throw "Expected GET $baseUri/internal to return 200, 401, or 403, but received $internalStatus."
}

Write-Host "Deployment smoke test passed: local groups, health, public root, and protected route responded as expected."