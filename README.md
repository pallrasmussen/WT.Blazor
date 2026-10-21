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

## Browser verification

CI installs Playwright Chromium and runs both public browser tests against a
locally started application with a unique disposable SQLite database. Run it
locally on Windows with:

```powershell
.\tests\Install-PlaywrightBrowsers.ps1
.\tests\Run-LocalBrowserSmoke.ps1
```

The runner creates a Development-only demo fixture, waits for health, captures
logs and TRX results, and always stops the process it started. No production
database is used. Windows-role browser tests still require isolated IIS staging
and a dedicated Windows identity per role; use Run-AuthenticatedBrowserSmoke.ps1.
The component suite also verifies a real two-context edit collision, preservation
of unsaved input, explicit reload, and successful retry for full and quick edit.

## Trend rules

All-history charts and direction indicators include recorded creation values and
waiting-time updates. Edits, reviews and lifecycle events do not invent data points.
The 90-day and 365-day filters end at the current UTC instant, rather than at the
latest update. Future-dated records are excluded from those windows. The last
record on each UTC day wins; consecutive equal values collapse to their latest
recorded date. A preceding record is used only during reduction and is not plotted
outside the selected window. No baseline is fabricated for legacy records missing
a creation entry. Demo entries remain visible until explicitly remediated after review.

## Build once and promote

Push a version tag such as `v0.2.0`, or run the Release package workflow with a
version. It runs validation, then publishes one package containing `app/`,
`operations/`, `deploy/`, `docs/`, and a manifest of SHA-256 hashes. Download both
the ZIP and its `.sha256` sidecar from the workflow artifacts. Archive approved
packages in your release store before GitHub's artifact retention expires.

```powershell
.\deploy\Expand-Release.ps1 -PackagePath C:\releases\WT.Blazor-0.2.0.zip -DestinationPath C:\releases\0.2.0
# Run on the staging server with the configured Windows groups and certificate:
C:\releases\0.2.0\deploy\Deploy-Test.ps1 -SourcePath C:\releases\0.2.0\app
# After staging acceptance, use the same verified bundle on production:
C:\releases\0.2.0\deploy\Deploy-Prod.ps1 -SourcePath C:\releases\0.2.0\app
```

Re-run extraction verification before promotion. Keep the original package
unchanged; deployment applies environment settings to the destination copy.
No workflow deploys to production automatically.

## Offline data review and recovery

Use the operations tool from the release bundle (or publish
`tools/WT.Blazor.Operations/WT.Blazor.Operations.csproj` locally). The tool opens
source databases read-only; the application does not expose these operations as
HTTP endpoints.

```powershell
.\deploy\Review-DemoHistory.ps1 -DatabasePath C:\backups\wt-blazor-20261021-020000.db -ReportPath C:\reviews\demo-history.json
.\deploy\Test-BackupRestore.ps1 -BackupPath C:\backups -WorkPath C:\recovery-drills
```

The demo report groups suspected entries (`Username=demo` or `Detail=Demo`) by
record and flags whether each is publicly active. An empty report means no known
demo markers were found, not proof that every historical value is authentic.
Review the report with the data owner; this tool never removes audit entries.

Recovery requires the matching `.keys` directory, a non-empty usable key ring,
and the `.db.probe` sidecar generated at backup time by the updated backup script.
Legacy backups remain untouched; take a new backup for full key-continuity verification.
It creates a unique restored copy, verifies SQLite integrity and foreign keys,
counts records/history/settings, checks migration history and file hashes, and
decrypts the backup-time probe with the restored keys. Keep drill directories access-restricted:
they contain restored data and keys. Run under an identity able to decrypt the
archived keys if keys are protected by Windows or a certificate. The tool does not
test live AD membership, IIS bindings, or real users' sessions; complete those checks
on isolated IIS staging with the existing authenticated browser scripts.

## Refreshing public results and resolving settings conflicts

Use **Refresh results** to reload waiting times, trend indicators and freshness
settings while preserving the selected hospital, department and search text.
The last-refreshed timestamp advances only after a successful load. Failed
refreshes retain the previous results and show an error. Filter changes clear
results from the old scope, and superseded requests cannot replace the latest
selection. Results are refreshed on demand; there is no background polling.

If another administrator changes portal settings, your unsaved input remains
visible. **Reload latest values** replaces it with the latest saved settings;
review those values before editing and saving again. Settings saves reload the
database row so a long-lived circuit does not remain stuck on an old version.

Audit sorts use the entry ID as a final tie-breaker when other values are equal.
This stabilizes ordering for tied timestamps; pages are not a frozen snapshot
when new entries are inserted between requests.
