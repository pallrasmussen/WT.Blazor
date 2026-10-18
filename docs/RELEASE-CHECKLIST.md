# WT.Blazor Release Checklist

## Before deployment

- [ ] After any migration change, run `dotnet test .\WT.Blazor.Tests\WT.Blazor.Tests.csproj --no-restore` before publishing.
- [ ] Run `tests\Test-WithCoverage.ps1` and keep total coverage at or above 80% line and 52% branch.
- [ ] Confirm the daily database backup and freshness-monitor scheduled tasks both ran successfully; respond to any stale/missing backup alert.
- [ ] Monthly, restore the newest database and matching Data Protection key archive in an isolated location, then confirm the SQLite integrity check succeeds.
- [ ] After `tests\Install-PlaywrightBrowsers.ps1`, run `tests\Run-BrowserSmoke.ps1 -BaseUrl http://SERVER-NAME:8081` for the public browser workflow.
- [ ] Run `dotnet ef migrations has-pending-model-changes --no-build`.
- [ ] Publish with `dotnet publish .\WT.Blazor.csproj -c Release -o ..\WT.Blazor-publish --no-restore`.
- [ ] Confirm the release manifest exists in `WT.Blazor-publish`.
- [ ] Confirm the real Viewer, Contributor, and Admin group names for the IIS command.
- [ ] On the isolated IIS staging site, verify Viewer, Contributor, and Admin workflows with dedicated test accounts before production deployment.

## IIS deployment

- [ ] Run `Setup-Iis.ps1` from an elevated Windows PowerShell session.
- [ ] Confirm the script reports a database backup when upgrading an existing site.
- [ ] Confirm the deployment smoke test passes for `/health`, `/`, and `/internal`.
- [ ] Confirm the IIS application pool is started and WebSockets are enabled.
- [ ] Confirm the backup and backup-monitor tasks are enabled and the backup destination is access-restricted.
- [ ] For HTTPS, provide both `-CertificateThumbprint` and `-HostName`, then confirm the HTTPS smoke test passes.

## Acceptance check

- [ ] Public users can browse, search, filter, open a trend, and print results.
- [ ] A Contributor can update a waiting time and see the audit history.
- [ ] An Admin can create, deactivate, and change portal settings.
- [ ] Confirm current green, yellow, and red freshness thresholds match the Settings page.
- [ ] Check `App_Data\logs` and the Windows Application event log for startup errors.