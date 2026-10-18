param(
    [string]$HealthUrl = "http://localhost:8081/health",
    [int]$FailureThreshold = 3,
    [string]$StatePath = "C:\ProgramData\WT.Blazor\health-state.json",
    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$From,
    [string[]]$To
)

if ($FailureThreshold -lt 1) {
    throw "FailureThreshold must be at least 1."
}

$stateDirectory = Split-Path -Parent $StatePath
New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null

$state = [pscustomobject]@{
    FailureCount = 0
    LastSuccessUtc = $null
    LastFailureUtc = $null
}
if (Test-Path $StatePath) {
    try {
        $state = Get-Content -Path $StatePath -Raw | ConvertFrom-Json
    }
    catch {
        Write-Warning "Could not read the previous health-monitor state. Starting with a clean state."
    }
}

try {
    $response = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 20
    if ($response.StatusCode -ne 200) {
        throw "Health endpoint returned HTTP $($response.StatusCode)."
    }

    $state.FailureCount = 0
    $state.LastSuccessUtc = [DateTime]::UtcNow.ToString("o")
    $state | ConvertTo-Json | Set-Content -Path $StatePath -Encoding UTF8
    Write-Host "PASS $HealthUrl"
    exit 0
}
catch {
    $state.FailureCount = [int]$state.FailureCount + 1
    $state.LastFailureUtc = [DateTime]::UtcNow.ToString("o")
    $state | ConvertTo-Json | Set-Content -Path $StatePath -Encoding UTF8

    $message = "WT.Blazor health check failed $($state.FailureCount) time(s). URL: $HealthUrl. Error: $($_.Exception.Message)"
    if ($state.FailureCount -eq $FailureThreshold) {
        if ($SmtpServer -and $From -and $To -and $To.Count -gt 0) {
            $mail = [System.Net.Mail.MailMessage]::new($From, $To[0])
            foreach ($recipient in $To | Select-Object -Skip 1) {
                $mail.To.Add($recipient)
            }
            $mail.Subject = "WT.Blazor health check failed"
            $mail.Body = $message

            $client = [System.Net.Mail.SmtpClient]::new($SmtpServer, $SmtpPort)
            $client.UseDefaultCredentials = $true
            try {
                $client.Send($mail)
            }
            finally {
                $client.Dispose()
                $mail.Dispose()
            }
        }
        else {
            Write-Warning "Failure threshold reached, but SMTP settings are incomplete. $message"
        }
    }

    throw $message
}