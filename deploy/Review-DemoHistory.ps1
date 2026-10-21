param(
    [Parameter(Mandatory)][string]$DatabasePath,
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$OperationsPath = (Join-Path $PSScriptRoot '../operations/WT.Blazor.Operations.dll')
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $ReportPath) { throw 'Choose a new report path; existing reports are not overwritten.' }
$report = dotnet $OperationsPath review-demo $DatabasePath
if ($LASTEXITCODE -ne 0) { throw 'Demo-history review failed.' }
$report | Set-Content -LiteralPath $ReportPath -Encoding utf8
Write-Host "Read-only report saved to $ReportPath. Review flagged rows before deciding on a remediation; no audit records were changed."
