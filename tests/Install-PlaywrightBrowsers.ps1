param()

$testProject = Join-Path $PSScriptRoot "..\WT.Blazor.Tests\WT.Blazor.Tests.csproj"
dotnet build $testProject
if ($LASTEXITCODE -ne 0) {
    throw "The test project did not build; Playwright browsers were not installed."
}

$installer = Join-Path $PSScriptRoot "..\WT.Blazor.Tests\bin\Debug\net10.0\playwright.ps1"
& $installer install chromium
if ($LASTEXITCODE -ne 0) {
    throw "Playwright Chromium installation failed."
}