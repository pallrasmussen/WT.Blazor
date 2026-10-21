# Operations playbook

The designated on-call operator owns the response to production alerts. If no
on-call rota exists, the deployment owner named in the release approval is the
operator. Record the incident, action taken, and final result with the release
or operations log.

## OTLP metrics

Set `OpenTelemetry__OtlpEndpoint` to the collector's absolute OTLP endpoint,
for example `http://otel-collector.internal:4317`. The standard
`OTEL_EXPORTER_OTLP_ENDPOINT` environment variable is also accepted. Do not
set both values to different collectors; application configuration takes
precedence. Restart the application pool after changing either value.

Export and alert on these counters from the `WT.Blazor` meter:

| Signal | Alert condition | First response |
| --- | --- | --- |
| `wt_blazor.public_refresh.failures` | Any sustained increase for 5 minutes | Check application logs, database health, and recent settings changes. |
| `wt_blazor.public_refresh.discarded` | More than 20 per minute for 15 minutes | Check browser/network latency and whether repeated refresh requests are expected. |
| `wt_blazor.circuits.disconnected` | More than 10 per minute or a sharp increase from baseline | Check IIS, WebSockets, proxy idle timeouts, and client network incidents. |

Counters are monotonically increasing. Configure alerts from rate/delta rather
than their absolute value. The collector, dashboards, and alert rules are
infrastructure-owned; validate them in staging before enabling production
notifications.

## Failed authenticated staging check

1. Confirm the self-hosted runner uses the intended dedicated Windows identity.
1. Check the runner can resolve and reach the isolated staging URL.
1. Inspect the workflow log and the staging application's `App_Data\logs`.
1. Verify the identity's Windows group membership and the IIS authentication and
   WebSocket configuration.
1. Rerun only the affected role after remediation. Do not disable the schedule
   to hide an unexplained failure.

## Failed or overdue recovery drill

1. Treat this as a high-priority recovery-readiness incident; the on-call
   operator acknowledges it immediately.
1. Inspect the scheduled task's last result and the backup/key archive access
   used by `Test-BackupRestore.ps1`.
1. Confirm the latest backup includes its non-empty matching `.keys` directory
   and `.db.probe` file.
1. Correct access, storage, or integrity failures, then manually run the drill
   against a fresh backup in an isolated work directory.
1. Record the verified row counts and key-continuity result before closing the
   incident. Escalate to the data owner if integrity or key continuity fails.

## Release acceptance failure

1. Do not promote the artifact.
1. Read the package smoke-test and release workflow logs.
1. Reproduce from the generated package rather than rebuilding the checkout.
1. Create a corrected versioned release only after validation passes; retain the
   failed artifact and logs for diagnosis.
