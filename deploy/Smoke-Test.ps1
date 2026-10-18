param(
    [string]$BaseUrl = "http://localhost:8081",
    [switch]$SkipProtectedChecks
)

$baseUri = $BaseUrl.TrimEnd('/')

function Test-Endpoint {
    param(
        [string]$Path,
        [string]$ExpectedContentType
    )

    $response = Invoke-WebRequest -Uri "$baseUri$Path" -UseBasicParsing -UseDefaultCredentials -TimeoutSec 30
    if ($response.StatusCode -ne 200) {
        throw "$Path returned HTTP $($response.StatusCode), expected 200."
    }
    if ($ExpectedContentType -and -not $response.Headers['Content-Type'].StartsWith($ExpectedContentType)) {
        throw "$Path returned content type '$($response.Headers['Content-Type'])', expected '$ExpectedContentType'."
    }
    Write-Host "PASS $Path"
}

Test-Endpoint -Path "/" -ExpectedContentType "text/html"
Test-Endpoint -Path "/health" -ExpectedContentType "text/plain"

if (-not $SkipProtectedChecks) {
    Test-Endpoint -Path "/internal" -ExpectedContentType "text/html"
    Test-Endpoint -Path "/api/history/export" -ExpectedContentType "application/vnd.ms-excel"
}

Write-Host "Smoke test completed successfully."