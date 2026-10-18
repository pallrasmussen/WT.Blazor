param(
    [ValidateRange(1, 100)]
    [int]$MinimumLineCoverage = 80,
    [ValidateRange(1, 100)]
    [int]$MinimumBranchCoverage = 52
)

$testProject = Join-Path $PSScriptRoot "..\WT.Blazor.Tests\WT.Blazor.Tests.csproj"
$coverageDirectory = Join-Path $PSScriptRoot "..\TestResults\coverage"
dotnet test $testProject --no-restore /p:CollectCoverage=true /p:CoverletOutput="$coverageDirectory\" /p:CoverletOutputFormat=cobertura /p:Threshold=$MinimumLineCoverage /p:ThresholdType=line /p:ThresholdStat=total
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$coverageReport = Join-Path $coverageDirectory "coverage.cobertura.xml"
if (-not (Test-Path $coverageReport)) { throw "Cobertura report was not generated: $coverageReport" }
[xml]$coverage = Get-Content $coverageReport
$branchCoverage = [Math]::Round([double]$coverage.coverage.'branch-rate' * 100, 2)
if ($branchCoverage -lt $MinimumBranchCoverage)
{
    throw "Branch coverage $branchCoverage% is below the required $MinimumBranchCoverage%."
}