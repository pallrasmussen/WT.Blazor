# Installing WT.Blazor on IIS

This guide installs WT.Blazor on a Windows IIS server with Windows Authentication. The supplied deployment script creates the IIS site and application pool, enables the required IIS features, preserves the SQLite database during upgrades, and verifies the deployment.

## Requirements

- Windows Server with IIS installed.
- Local Administrator access on the IIS server.
- Network access to download the .NET 10 ASP.NET Core Hosting Bundle, or a preinstalled Hosting Bundle.
- Three Windows or Active Directory security groups:
  - Viewer: public staff access where applicable.
  - Contributor: access to the internal administration area.
  - Admin: contributor access plus Settings and removal privileges.
- A TLS certificate with a private key installed under `Cert:\LocalMachine\My` for the environment host name.
- DNS records for the environments: `btdev.heilsunet.fo`, `bttest.heilsunet.fo`, and `bt.heilsunet.fo`.
- An HTTP port for the local IIS binding. The script defaults to `8081`; public access uses HTTPS on `443`.

The script enables IIS Windows Authentication and WebSockets. WebSockets are required for reliable Blazor Server interaction.

## Publish the application

On the build workstation, from the repository root, run:

```powershell
dotnet publish .\WT.Blazor.csproj -c Release -o ..\WT.Blazor-publish --no-restore
```

Do not copy the `WT.Blazor` source project to the IIS server. Copy the `deploy` folder and the published output only. They can be stored anywhere; for example:

```text
C:\Releases
|- deploy
|  |- Setup-Iis.ps1
|  |- Deploy-Dev.ps1
|  |- Deploy-Test.ps1
|  `- Deploy-Prod.ps1
`- WT.Blazor-publish
  `- ... published files ...
```

On Test and Production, this layout makes the wrapper default `-SourcePath` to `C:\Releases\WT.Blazor-publish`. When `Deploy-Dev.ps1` is run from the source repository, it defaults to the sibling publish output created by `dotnet publish -o ..\WT.Blazor-publish`.

## First installation

1. Copy `C:\Releases\deploy` and `C:\Releases\WT.Blazor-publish` from `HNPV-WAC05` to the target IIS server.
1. Open **Windows PowerShell as Administrator** on the IIS server and run the matching environment wrapper. It creates the IIS application pool, site, folder, HTTP binding, and HTTPS/SNI binding automatically:

```powershell
Set-Location C:\Releases\deploy

.\Deploy-Test.ps1
```

1. In IIS Manager, select the certificate for the created HTTPS binding and configure **SSL Settings > Client certificates** as **Ignore**.

The deployment completes in one wrapper run. It skips the HTTPS smoke test only when it creates a new binding without a certificate; after selecting the certificate in IIS Manager, verify the site in a browser. Certificate issuance and renewal remain entirely in IIS Manager; do not supply `-CertificateThumbprint` for this workflow.

These are local security groups on the IIS server. The group names are `WT-Viewers`, `WT-Contributors`, and `WT-Admins`; `$env:COMPUTERNAME` qualifies them for the current server. The script uses these local groups by default, so the three group arguments may be omitted. Do not use placeholder values such as `YOUR-ADMIN-GROUP`; the script rejects them. The older misspelling `-ContributerGroup` is accepted for compatibility, but new commands should use `-ContributorGroup`.

`-SuperAdminGroup` is no longer supported. Use `-AdminGroup`; members of that group have super-administrator access. The script also configures `AllowedHosts` as `localhost` plus the server name and, for HTTPS, the certificate host name. Supply `-AllowedHosts 'waiting-times.example.org;waiting-times'` when users browse through additional DNS aliases.

HTTPS is required by default. The script only permits HTTP when `-AllowInsecureHttp` is supplied for local testing; do not use that switch for Dev, Test, or Production.

## Environment deployment wrappers

Publish on `HNPV-WAC05`, then manually copy the published folder and the `deploy` folder to the target server. Run the elevated wrapper on that target server. Each wrapper sets the HTTPS host name and allowed-host list, then verifies that IIS already has the matching HTTPS binding. With the documented `C:\Releases` layout, no parameters are needed:

```powershell
.\Deploy-Dev.ps1
.\Deploy-Test.ps1
.\Deploy-Prod.ps1
```

The wrappers do not renew certificates. Manage certificate renewal in IIS Manager; subsequent wrapper runs preserve the updated binding. The wrappers use the server-local `WT-Viewers`, `WT-Contributors`, and `WT-Admins` groups by default. Override them only when the target server uses different group names:

```powershell
.\deploy\Deploy-Test.ps1 `
  -ViewerGroup 'HEILSUNET\BT-Test-Viewers' `
  -ContributorGroup 'HEILSUNET\BT-Test-Contributors' `
  -AdminGroup 'HEILSUNET\BT-Test-Admins'
```

The environments use separate IIS servers, so their local IIS resource names can reflect the deployed application name:

| Environment | IIS server | IIS site | Application pool | Site folder | HTTP port |
| --- | --- | --- | --- | --- |
| Dev | `HNPV-WAC05` | `WT.Blazor` | `WT.BlazorPool` | `C:\inetpub\wwwroot\WT.Blazor` | `8081` |
| Test | `HNTV-DEV02` | `WT` | `WTPool` | `C:\inetpub\wwwroot\WT` | `8082` |
| Production | `HNPV-WEB02` | `WT` | `WTPool` | `C:\inetpub\wwwroot\WT` | `8083` |

The Dev wrapper now uses the existing Dev application defaults (`WT.Blazor`, `WT.BlazorPool`, and `C:\inetpub\wwwroot\WT.Blazor`), so no compatibility overrides are required.

By default, setup creates:

| Setting | Default |
| --- | --- |
| IIS site | `WT.Blazor` |
| Application pool | `WT.BlazorPool` |
| Site folder | `C:\inetpub\wwwroot\WT.Blazor` |
| HTTP port | `8081` |
| Database backups | `C:\inetpub\backups\WT.Blazor` |

Override these as needed:

```powershell
.\deploy\Setup-Iis.ps1 `
  -SiteName 'WaitingTimes' `
  -AppPoolName 'WaitingTimesPool' `
  -PhysicalPath 'D:\Sites\WaitingTimes' `
  -SourcePath 'D:\Releases\WT.Blazor-publish' `
  -Port 8081 `
  -BackupPath 'D:\Backups\WaitingTimes' `
  -ViewerGroup "$env:COMPUTERNAME\WT-Viewers" `
  -ContributorGroup "$env:COMPUTERNAME\WT-Contributors" `
  -AdminGroup "$env:COMPUTERNAME\WT-Admins"
```

The first application start creates and migrates `App_Data\wt-blazor.db` automatically.

## What the setup script configures

- Enables IIS, Windows Authentication, and WebSockets.
- Installs the .NET 10 ASP.NET Core Hosting Bundle when the ASP.NET Core Module is absent.
- Creates a **No Managed Code** application pool with Always Running startup and no idle or periodic recycle timeout.
- Enables both Windows and Anonymous Authentication. Anonymous access is intentional for the public waiting-times page; application authorization protects the internal pages.
- Grants the application-pool identity Modify permission on the site folder, including `App_Data` for SQLite, logs, and temporary exports.
- Sets the configured local Windows security groups in the deployed `appsettings.json`.
- Sets the backup path, HTTPS redirect setting, and allowed hosts in the deployed `appsettings.json`.
- Calls the deployment smoke test after startup, including verification that the configured Viewer, Contributor, and Admin groups exist as local groups on the IIS server.

## Updating an existing installation

1. Publish the new release.
1. Copy the new `WT.Blazor-publish` folder to the server.
1. Run the elevated environment wrapper with the same group and resource overrides used for that environment.

Before replacing release files, the script backs up the SQLite database. It synchronizes the published files with `robocopy /MIR` while excluding `App_Data`, so the database, logs, data-protection keys, and export working files are retained. EF Core applies any included migrations when the application starts.

The backup script retains the newest 30 database backups by default. To create a manual backup:

```powershell
.\deploy\Backup-Database.ps1 `
  -AppPoolName 'WT.BlazorPool' `
  -DatabasePath 'C:\inetpub\wwwroot\WT.Blazor\App_Data\wt-blazor.db' `
  -BackupPath 'C:\inetpub\backups\WT.Blazor'
```

## Verification

The setup script checks these endpoints automatically:

| URL | Expected response |
| --- | --- |
| `https://ENVIRONMENT-HOST/health` | `200` |
| `https://ENVIRONMENT-HOST/` | `200` |
| `https://ENVIRONMENT-HOST/internal` | `200`, `401`, or `403` |

Run the smoke test manually when needed:

```powershell
.\Test-Deployment.ps1 -Scheme https -HostName btdev.heilsunet.fo -Port 443
```

Then browse to the environment HTTPS URL, verify the public page, and sign in with a member of each relevant local security group to confirm its expected access.

For release testing, use a separate IIS staging site and database with disposable Viewer, Contributor, and Admin accounts. Run the public browser smoke test against that URL:

```powershell
.\tests\Run-BrowserSmoke.ps1 -BaseUrl https://bttest.heilsunet.fo
```

Then verify the authenticated workflows under their corresponding Windows identities: Contributor edit/review, Admin create/deactivate/reactivate/settings, and history exports. Do not run these data-changing checks against the production database.

Run the role-scoped permission smoke test once for each dedicated staging identity. The PowerShell process launching Chromium must already be authenticated as that identity:

```powershell
.\tests\Run-AuthenticatedBrowserSmoke.ps1 -BaseUrl https://bttest.heilsunet.fo -Role Viewer
.\tests\Run-AuthenticatedBrowserSmoke.ps1 -BaseUrl https://bttest.heilsunet.fo -Role Contributor
.\tests\Run-AuthenticatedBrowserSmoke.ps1 -BaseUrl https://bttest.heilsunet.fo -Role Admin
```

If the site is accessed from another machine, permit the selected port through the server firewall and any network firewall.

## HTTPS and Windows authentication

The deployment requires HTTPS. Create and renew the certificate and hostname binding in IIS Manager before running a wrapper. The wrapper verifies that binding, enables `Hosting:RedirectToHttps`, and smoke-tests the HTTPS URL; it does not manage certificates. The server must be able to resolve the supplied host name and trust the certificate.

In **IIS Manager > site > SSL Settings**, configure client certificates as **Ignore**. Do not use **Accept** or **Require**: this application authenticates staff with Windows Integrated Authentication, not TLS client certificates. Using **Accept** causes TLS renegotiation and can make Edge show `ERR_CONNECTION_RESET`.

For Windows Integrated Authentication, register an HTTP service principal name (SPN) for each HTTPS alias against the computer account of the IIS server that hosts that environment. Run these from a domain-administration workstation or domain controller with permission to update AD SPNs:

```powershell
setspn -S HTTP/btdev.heilsunet.fo HEILSUNET\<DEV-IIS-SERVER>$
setspn -S HTTP/bttest.heilsunet.fo HEILSUNET\<TEST-IIS-SERVER>$
setspn -S HTTP/bt.heilsunet.fo HEILSUNET\<PROD-IIS-SERVER>$
```

If all environments share one IIS server, use that computer account for each command. Do not include `:443` in the SPN. Verify each registration with:

```powershell
setspn -Q HTTP/btdev.heilsunet.fo
setspn -Q HTTP/bttest.heilsunet.fo
setspn -Q HTTP/bt.heilsunet.fo
```

After a new SPN is registered, clients can run `klist purge` and reopen the browser before retrying. The expected result is transparent Windows sign-in; the application then checks membership of the configured Viewer, Contributor, and Admin groups.

The live settings refresh feature is designed for one IIS application instance. If the application is later scaled across multiple servers, use shared cache invalidation or a message bus so settings changes propagate between instances.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `500.30` on startup | Run `Test-Deployment.ps1`; it displays recent ASP.NET Core Module and .NET Runtime events. Also inspect `App_Data\logs\wt-blazor-*.log`. |
| Blank or non-interactive pages | Confirm the IIS WebSockets feature is enabled and the `WT.BlazorPool` is started. |
| `401` or `403` on `/internal` | Confirm Windows Authentication is enabled and the user belongs to the configured Contributor or Admin group. |
| Repeated Windows credential prompt or IIS `401.1` on an HTTPS alias | Verify the matching `HTTP/<host>` SPN is registered to the IIS server computer account. Confirm the browser is using the correct domain identity. |
| Edge shows `ERR_CONNECTION_RESET` after changing IIS SSL Settings | Set **Client certificates** to **Ignore**. The application does not use TLS client certificates. |
| Database or export write errors | Confirm `IIS AppPool\WT.BlazorPool` has Modify permission on the application folder. |
| Setup cannot find the release | Verify `-SourcePath` points to the published folder, not the source project. |
| Setup reports a missing HTTPS binding | Configure the environment host binding in IIS Manager before running the wrapper. The script checks this before it stops an existing application pool. |
| Hosting Bundle install fails | Download the .NET 10 ASP.NET Core Hosting Bundle manually from `https://dotnet.microsoft.com/download/dotnet/10.0`, install it, restart IIS, and rerun setup. |

The protected deployment metadata endpoint is available to super-administrators at `/diagnostics/deployment`.