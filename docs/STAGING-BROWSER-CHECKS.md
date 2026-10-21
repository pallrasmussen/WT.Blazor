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
Until `STAGING_BROWSER_ENABLED` is `true`, scheduled jobs are skipped. Use
**Run workflow** to test one role before enabling the schedule; supply that
runner's label and the isolated staging URL as inputs.
