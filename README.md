# WT.Blazor

Hospital waiting-time portal built with .NET 10, interactive server Blazor,
Entity Framework Core and SQLite. Public users can browse waiting times and
active records' trends. Staff use Windows authentication to maintain records.
The interface supports Faroese and English.

## Local development

Install the .NET 10 SDK, then run:

```powershell
dotnet restore .\WT.Blazor.sln
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

The application currently uses SQLite and is intended for a single application
instance with its database on durable local storage. If deployment requires
multiple application instances, higher write concurrency, or database-level
high availability, plan a move to a server database such as SQL Server or
PostgreSQL. This requires provider and migration work; changing only the
connection string is not sufficient.

## Verification

```powershell
dotnet test .\WT.Blazor.sln
dotnet format .\WT.Blazor.sln --verify-no-changes
.\tests\Test-WithCoverage.ps1
```

CI validates pull requests and pushes to master, audits NuGet dependencies,
checks formatting, runs deployment-script tests, and requires 80% line and
52% branch coverage. CodeQL scans C# and GitHub Actions on pull requests,
pushes, and weekly.
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
and a dedicated Windows identity per role. Configure the repository variables
documented in [staging browser checks](docs/STAGING-BROWSER-CHECKS.md) to run
them on a schedule, or dispatch the workflow with a runner label and staging URL.
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
the ZIP and its `.sha256` sidecar from the GitHub Release. Tagged releases also
include an SPDX SBOM and build provenance attestation. Workflow artifacts remain
available for manually dispatched pre-release packages.

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
.\deploy\Install-RecoveryDrillTask.ps1 -BackupPath C:\backups -OperationsPath C:\releases\0.2.0\operations\WT.Blazor.Operations.dll
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

The recovery-drill installation script schedules an isolated verification every
four weeks and a paired monitor. Provide SMTP arguments to receive an alert for
an overdue or failed drill. It runs under `SYSTEM`; make sure that identity can
read the backup, matching key archive, and configured drill directory.

## Operational telemetry and response

Set `OpenTelemetry__OtlpEndpoint` to the absolute URI of the OTLP collector to
export public-refresh and Blazor circuit metrics. The standard
`OTEL_EXPORTER_OTLP_ENDPOINT` environment variable is supported as a fallback.
Leave both unset to disable metric export. See the
[operations playbook](docs/OPERATIONS-PLAYBOOK.md) for alert thresholds and
response procedures for telemetry, staging verification, recovery drills, and
release-acceptance failures.

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

## Trend downloads and duration limits

Trend PDFs use the period currently displayed by the chart: all history, 90 days,
or 365 days. Filtered downloads include the period in the filename and PDF header.
The PDF endpoint accepts `periodDays=90` or `periodDays=365`; omit the parameter for
all history. Unsupported values return HTTP 400. Changing a trend period hides
the previous chart and export until the latest request succeeds. Failures show a
retry action, and leaving the page cancels its outstanding work.

Waiting times must fit in a signed 32-bit count of canonical days. Limits are
2,147,483,647 days, 306,783,378 weeks, or 71,582,788 months (30 days per month).
Creation and both editing forms enforce the selected unit's limit; server-side
validation rejects overflowing values before changing records or audit history.
Existing stored values are not rewritten by this change.

## Waiting-time freshness

`WaitingTime.VerifiedAt` records confirmation of an available estimate on creation,
a change to its value/unit, resumption, or an explicit **Mark reviewed** action. Editing names, hospital/department
metadata or notes, and reactivating a record, do not refresh that verification timestamp.
Their actual edit times remain in the immutable audit history. After reactivation,
review the estimate explicitly if it has been verified. The migration preserves `UpdatedAt` and reconstructs `VerifiedAt` only where audit
evidence exists; staff should explicitly review estimates lacking that evidence.

## Estimates for newly referred patients

The portal publishes staff estimates for newly received **routine referrals**,
not measured waits for individual patients. Each record can specify the endpoint:
first consultation, investigation, or start of treatment. Public cards, trend
pages and trend PDFs explain that actual waits depend on clinical priority and
individual circumstances. The existing public note is labelled as referral
conditions / the patient groups covered. Do not enter patient-identifiable data.

Staff can assign a responsible department in the full create/edit forms. That
field stays internal; it is also snapshotted into the staff audit history.
Unassigned ownership and unspecified endpoints remain visible to staff so legacy
records can be reviewed without inventing their meaning.

New forms start with **No estimate available**. Choose **Estimate available** to
enter a numeric value and unit. **Service currently unavailable** is separate
from an unknown estimate and from deactivation (which removes the public card).
An available estimate of zero is valid. Placeholder numeric storage for other
statuses is never presented as a zero estimate or included in trends/long waits.

Changing the referral endpoint or switching availability starts a new trend
series. Published estimate changes are plotted only for the current comparable
series; old entries remain in the audit history. A note/owner edit does not start
a new series or verify an estimate. Endpoint-only edits do not verify it either.
The review queue includes available estimates lacking a verification date.

The additive `AddReferralEstimateDefinition` migration preserves existing values,
marks their endpoints as unspecified and owners as unassigned, and backfills
verification dates only from recorded creation, waiting-time updates and explicit
reviews with numeric evidence. Ordinary edits, future events and known demo markers do not qualify;
records without evidence show **Verification date not recorded**. Audit entries
are not rewritten. Back up the database before deployment, apply the migration
on staging, then have data owners define legacy endpoints/ownership and explicitly
review estimates. Older binaries cannot represent the new statuses correctly;
roll back by restoring the pre-upgrade database and matching application bundle.

## Review queues and unusually large changes

The staff review queue includes active records with missing verification dates,
overdue verification, no responsible department, or an unspecified referral
endpoint. Missing verification comes first, then overdue records, then incomplete
metadata; ties use the oldest review date, name and ID. Staff can filter by reason
and responsible department. **Still correct** records a review without changing
the value. It does not fill missing metadata, so incomplete records remain in the
queue. Bulk confirmation reports the number actually reviewed, excluding conflicts.

Updates that at least double or halve an available estimate and change it by at
least 28 canonical days require explicit confirmation in both editing forms and
on the server. Unit-only conversions do not trigger the warning. Changing the
value/unit after checking confirmation invalidates the acknowledgement. Confirmed
large changes are noted in audit history; concurrency protection still applies.

## Hospital comparisons

Staff may assign an optional comparison procedure code to records representing
the same clinical procedure at different hospitals. Use an agreed clinical code;
do not group procedures merely because their names look similar. Comparisons
require an available, verified estimate, a specified referral endpoint and
explicit identical patient conditions (the public note). Blank conditions are
not assumed equivalent. Different conditions/endpoints, unknown or inactive
records, shared waiting lists, and duplicate hospital entries are excluded.

Comparable results are shown together on the public page using canonical days,
with verification dates and overdue warnings. Comparisons follow the selected
public filters; choose all hospitals to see cross-hospital results. They do not
guarantee referral choice or clinical suitability. Codes and responsible
owner fields are internal and are not printed in public comparison cards.

## Patient guidance publication

Administrators can edit referral instructions and contact guidance in Faroese and
English under Settings. The four fields are limited to 2,000 characters each and
rendered as plain text. Obtain local clinical approval for wording and contact
details before selecting **Publish these approved instructions** and saving.
All four fields are required to publish; publication and text changes use the
existing settings concurrency and immutable audit history. Unchecking publication
removes the approved text from public pages. No contact numbers are invented.

The public page always explains that the portal cannot show individual queue
positions or appointment status, directs individual questions to the referring
clinician/relevant department, and discourages sending medical information through
the portal. Without published local text, these generic instructions remain.

## Accessibility verification

The disposable local browser runner now performs axe-core WCAG A/AA checks in both
languages, checks 320px reflow with enlarged text as well as desktop layouts, and
verifies keyboard focus stays in editing dialogs and returns on Escape. It also
checks the comparison/large-change/guidance workflow using synthetic records.
CI uploads axe JSON reports and failure screenshots alongside TRX results.
These checks complement manual assistive-technology and clinical usability review;
they do not establish complete accessibility conformance.

Run `tests/Run-LocalBrowserSmoke.ps1` for these checks. Mutating workflow tests and
staff-only browser checks require `RUN_MUTATING_BROWSER_TESTS=1`, set only by the
runner for its disposable Development database. Never enable this flag against a
real deployment. Tests share a browser-fixture collection to avoid simultaneous
fixture mutations. For an existing system Chromium on Linux, the optional
`PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` test variable selects that executable.

Back up and test the additive `AddComparisonsAndPatientGuidance` migration on
staging before deployment. Existing comparison codes/guidance remain blank and
publication remains disabled; clinical staff must deliberately configure them.

### Estimate expiry and optional ranges

Administrators can set **Estimate expiry (months)** in Settings (1–60 months,
default 12). This is independent of the earlier freshness/review indicators.
At the exact calendar-month boundary after `VerifiedAt`, the public card and
current-value text in trend/PDF reports show an expired message and direct the
patient to the relevant department. Available estimates with no verification,
or an invalid future verification, are also hidden. Staff retain the stored
values for review. Metadata edits do not renew verification. Explicit review or
a changed clinical estimate renews it. Historical chart values remain labelled
as history; expired estimates are excluded from hospital comparisons.

Staff can optionally enter an upper waiting-time bound in the same unit as the
existing value, making the existing value the lower bound (for example, 6–10
weeks). Leave the upper bound blank for a single value. The upper bound must be
at least the lower bound and fit the supported unit limits. Enter only a range
supported by the department; no statistical interval is invented. Either bound
can trigger large-change confirmation. Both bounds survive metadata edits,
undo, history snapshots, and history downloads; hospital comparisons convert
both to canonical days. Long-wait counts use the upper bound.

Ranges are not plotted as lower-bound-only points or given a directional arrow.
Switching between a range and a single value starts a fresh trend series; range
changes remain in the internal audit history. Existing estimates and historical
values are preserved by the additive `AddEstimateExpiryAndRanges` migration;
upper bounds start null and existing settings receive a 12-month expiry. Back up
and stage the migration before deployment, and review the expiry policy with the
responsible departments.

### Automated backup recovery acceptance

The disposable Windows browser runner now also publishes the operations tool,
creates a consistent SQLite snapshot (including committed WAL data), archives
matching Data Protection keys/probe, and runs the real `Test-BackupRestore.ps1`
against an isolated copy. The recovery JSON report is uploaded with the browser
artifacts. This is part of Validate CI. Unit/integration checks
also restore estimates with ranges, ownership/verification, history and portal
settings, verify wrong-key rejection, and ensure snapshot paths cannot overwrite
existing files. Recovery now materializes all application entities so a database
with unreadable application columns cannot pass on table counts alone.

Use the operations bundle from the matching release when validating an actual
backup. Run `deploy/Test-BackupRestore.ps1` against the backup directory and
retain its result as deployment evidence; the existing scheduled recovery-drill
installer and monitor support periodic checks. CI verifies synthetic recovery,
not the contents of a hospital's real backups. Real IIS permission acceptance is
documented in [STAGING-BROWSER-CHECKS.md](docs/STAGING-BROWSER-CHECKS.md).
