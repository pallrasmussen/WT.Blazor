#Requires -RunAsAdministrator
param(
    [string]$TaskName = "WT.Blazor Health Monitor",
    [string]$HealthUrl = "http://localhost:8081/health",
    [int]$IntervalMinutes = 5,
    [int]$FailureThreshold = 3,
    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$From,
    [string[]]$To
)

if ($IntervalMinutes -lt 1) {
    throw "IntervalMinutes must be at least 1."
}

$monitorScript = Join-Path $PSScriptRoot "Monitor-Health.ps1"
if (-not (Test-Path $monitorScript)) {
    throw "Health monitor script not found: $monitorScript"
}

$arguments = @(
    "-NoProfile",
    "-ExecutionPolicy Bypass",
    "-File `"$monitorScript`"",
    "-HealthUrl `"$HealthUrl`"",
    "-FailureThreshold $FailureThreshold",
    "-SmtpPort $SmtpPort"
)
if ($SmtpServer) { $arguments += "-SmtpServer `"$SmtpServer`"" }
if ($From) { $arguments += "-From `"$From`"" }
foreach ($recipient in $To) { $arguments += "-To `"$recipient`"" }

$taskCommand = "PowerShell.exe $($arguments -join ' ')"
& schtasks.exe /Create /TN $TaskName /TR $taskCommand /SC MINUTE /MO $IntervalMinutes /RU SYSTEM /RL HIGHEST /F | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Could not register scheduled task '$TaskName'."
}
Write-Host "Health monitor task '$TaskName' runs every $IntervalMinutes minute(s)."