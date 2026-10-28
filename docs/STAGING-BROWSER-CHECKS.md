# Authenticated staging browser checks

The `Authenticated staging browser checks` workflow validates the Viewer,
Contributor, and Admin UI boundaries against an isolated IIS staging site. It
does not use credentials or transmit them to GitHub: each self-hosted Windows
runner must run as the dedicated test identity for its role.

To enable the weekday schedule, create these repository variables:

| Variable | Value |
| --- | --- |
| `STAGING_BROWSER_ENABLED` | `true` |
| `STAGING_BROWSER_BASE_URL` | The HTTPS URL of the isolated staging site |
| `STAGING_VIEWER_RUNNER_LABEL` | Label on the Viewer-identity Windows runner |
| `STAGING_CONTRIBUTOR_RUNNER_LABEL` | Label on the Contributor-identity Windows runner |
| `STAGING_ADMIN_RUNNER_LABEL` | Label on the Admin-identity Windows runner |

Each runner must also have the `self-hosted` and `windows` labels, access only
to the isolated staging site, and permission to launch Playwright Chromium.
Missing or disabled configuration now fails a hosted configuration job with an
actionable error instead of silently skipping the scheduled run. The three role
labels must be distinct; generic labels such as `windows` are rejected. Role jobs
require `self-hosted`, `Windows`, and the configured role label, time out after
20 minutes, and upload TRX results per role. Use
**Run workflow** to test one role before enabling the schedule; supply that
runner's label and the isolated staging URL as inputs.

The repository cannot provision AD identities, IIS bindings, or self-hosted runners.
After configuring these externally, dispatch each role once and confirm its TRX
contains one passed role test (the other two roles are intentionally skipped).
Then enable the schedule and verify all three role jobs pass on a scheduled run.
Viewer checks must reach an access-denied page and receive HTTP 403 for a staff
export; an unauthenticated HTTP 401 must fail the check.

## Verify the real identity and all permission boundaries

The checks now require `STAGING_VIEWER_IDENTITY`, `STAGING_CONTRIBUTOR_IDENTITY`,
and `STAGING_ADMIN_IDENTITY` repository variables, each containing the dedicated
`DOMAIN\account` used by its runner. The identities must be different. Viewer
and Contributor accounts must not inherit the Admin group or local Administrators
membership. The check rejects accidental privilege escalation as well as missing
permissions.

Choose **All** in a manual workflow run to check all three configured role
runners in one run. For a single role, supply `runner_label`, `base_url`, and
`expected_identity`. Scheduled and All runs use the repository identity/runner
variables. Staging must run the code containing `/diagnostics/authentication`.
This endpoint requires Viewer access, uses `Cache-Control: no-store`, and returns
only the caller's identity, Windows/IIS hosting flags and effective permissions;
it never lists directory groups or other accounts.

Each role test first requires the exact configured identity, a real Windows
identity, IIS hosting, and the expected effective permissions. It then checks
server responses as well as UI controls: Viewer gets 403 for internal editing,
settings, deployment diagnostics and staff CSV export; Contributor can edit and
export but gets 403 for settings and deployment diagnostics; Admin can access
those administrator surfaces and lifecycle controls. These checks do not change
staging records. Keep at least one active synthetic estimate in staging so edit
and lifecycle controls can be inspected.

To run locally from PowerShell 7 as a dedicated identity:

```powershell
.\tests\Run-AuthenticatedBrowserSmoke.ps1 -BaseUrl https://waiting-times-staging.example.org -Role Viewer -ExpectedIdentity 'DOMAIN\wt-viewer-test'
```

A successful local Development run is not evidence of Windows/IIS authentication.
The workflow and tests fail on missing required configuration; configuring AD,
IIS and role-specific runners remains an installation task. Review all three
passed role results before accepting the deployment.
