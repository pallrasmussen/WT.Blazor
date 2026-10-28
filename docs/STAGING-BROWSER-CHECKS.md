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
