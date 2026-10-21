# WT.Blazor

Hospital waiting-time portal built with .NET 10, interactive server Blazor,
Entity Framework Core and SQLite. Public users can browse waiting times and
active records' trends. Staff use Windows authentication to maintain records.
The interface supports Faroese and English.

## Local development

Install the .NET 10 SDK, then run:

```powershell
dotnet restore .\WT.Blazor.Tests\WT.Blazor.Tests.csproj
dotnet run --project .\WT.Blazor.csproj
```

Use the Development launch profile for the development authentication handler.
Never expose a deployment running in Development to untrusted users.
Database migrations and portal settings initialization run at startup.
Production starts without sample waiting times or synthetic trend history.

To explicitly populate a disposable development database with demo data:

```powershell
$env:SeedData__EnableDemoData = "true"
dotnet run --project .\WT.Blazor.csproj
```

Demo data requires both Development and this flag. Do not enable it against
real data. Existing demo audit entries are preserved; review them separately
before release rather than deleting immutable audit history automatically.

## Configuration and access

Configure `ConnectionStrings:DefaultConnection`, `AdGroups:Viewer`,
`AdGroups:Contributor`, and `AdGroups:Admin` for the target server.
Viewer permits internal viewing, Contributor permits updates and history
exports, and Admin permits creation, deactivation and portal settings changes.
See the policy implementation for the additional super-admin assertion.
Inactive records are excluded from public trend pages and PDF reports.

`appsettings.json` also controls backup paths, audit review age, export limits,
temporary export storage, HTTPS redirection and logging. Database operations
sharing a Blazor circuit's scoped context are serialized across services.

## Verification

```powershell
dotnet test .\WT.Blazor.Tests\WT.Blazor.Tests.csproj
dotnet format .\WT.Blazor.csproj --verify-no-changes
dotnet format .\WT.Blazor.Tests\WT.Blazor.Tests.csproj --verify-no-changes
.\tests\Test-WithCoverage.ps1
```

CI validates pull requests and pushes to master, audits NuGet dependencies,
checks formatting, and requires 80% line and 52% branch coverage.
Browser smoke tests require Playwright installation; see `tests/`.

## Deployment

Follow [IIS deployment](docs/IIS-DEPLOYMENT.md) and the
[release checklist](docs/RELEASE-CHECKLIST.md), including backup restoration,
role-based staging verification and browser smoke tests.
