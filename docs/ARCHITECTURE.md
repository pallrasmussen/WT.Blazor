# Estimate editing and publication

`Models/WaitingTimeInput.cs` defines editable estimate data. The create and update
commands inherit that data and add audit context, record identity, concurrency
version and confirmation. Use named properties, or construct a command from an
existing input; there is no long positional constructor to keep synchronized.

```csharp
var command = new WaitingTimeUpdate(WaitingTimeInput.From(item))
{
    Id = item.Id,
    ExpectedVersion = item.Version,
    Username = username,
    WaitValue = 6,
    WaitUpperValue = 10,
    Unit = WaitUnit.Vikur
};
var saved = await manager.UpdateAsync(command);
```

`WaitingTimeValidation` owns normalization, required fields, enum checks, duration
bounds and text limits. `PortalSettingsValidation` owns settings and guidance
rules. Forms can report those errors before submitting; services always validate
again. These validators return typed errors with no dependency on a language or
Blazor. Classes under `Localization` turn errors and estimate data into readable
text. `ReferralEstimate` contains review-date and expiry decisions;
`ReferralEstimateText` contains translated status, endpoint and duration text.

`WaitingTimeManagementService` owns persisted estimate changes, verification
dates, trend-series changes, versions and audit entries. Every mutation returns
a detached copy of the saved entity. The UI replaces its record with that result. It must not increment a version, infer a verification
date or reproduce a clinical update after calling the service. A no-op update
returns the existing version and produces no new history entry.

# Administration components

`Internal.razor` and its code-behind coordinate authentication, catalog loading,
filter URLs, lifecycle actions, bulk review and undo. They delegate editing to
components in `Components/Administration`:

- `AdministrationFilters` renders hospital, department, search and review controls
  against `AdministrationFilterState`. It requests catalog refreshes separately
  from local filtering so typing a search does not trigger database reloads.
- `WaitingTimeCreateForm` owns its draft, early validation and create request.
- `WaitingTimeEditDialog` and `WaitingTimeQuickEditor` provide the two editor
  layouts. `WaitingTimeEditorBase` owns their common validation, dirty-state,
  confirmation, save, conflict and discard workflow. Drafts reset only when the
  record ID or version changes; unrelated parent renders preserve unsaved work.
  The page retains `WaitingTimeEditorSession` for a quick edit so filtering its
  row out and back in preserves the draft and confirmation state too.
- `WaitingTimeEditPreview` formats the before/after values without making
  persistence decisions.

A saved edit returns both the authoritative saved entity and an immutable input
snapshot from before editing. Undo sends that snapshot back through the same
management service with the saved version. It therefore follows normal
validation, audit and optimistic concurrency rules.

The existing browser selectors, dialog semantics and focus behavior are preserved.
Application tests cover server-owned values and versions, range confirmation,
validation, conflicts and history; browser checks cover real form submission,
keyboard interaction and accessibility. This refactoring requires no database
migration.

# Reading and public presentation

Catalog, audit history, operations summaries and trend queries have separate
implementations: `WaitingTimeCatalogService`, `HistoryReadService`,
`OperationsReadService` and `TrendReadService`. Each implements its existing
interface and uses the same scoped `ApplicationDbContext`. All query entry points
retain `DatabaseOperation.EnterAsync`; separate service instances must still
serialize work when they share one context. Queries keep their existing database
filtering, paging, ordering, tie-breakers and aggregation behavior.

`Home` owns loading and filtering public results and building return URLs.
`PublicWaitingTimeCard` owns the card markup, expiry/verification display and
trend indicator. It takes the current record, settings, clock value, optional
trend change and link. It makes no data requests and preserves the existing DOM
structure and accessibility labels.

# Audit snapshots, limits and exports

`WaitingTimeHistorySnapshot` is the single mapper for immutable audit metadata
and before/after duration values. The management service captures the previous
duration before updating a record and supplies request/user/time context. The
mapper retains ranges, display units and canonical days, including the
distinction between an unavailable estimate and an available zero. Lifecycle
actions use the same mapper without rewriting prior history.

`FieldLimits` provides the existing string limits for model attributes, EF
configuration, shared validation and HTML inputs. Values and schema metadata are
unchanged. A future limit change must still be reviewed as a potential schema
migration, even though its code references are centralized.

History export preparation lives in `HistoryExportReportReader`: it performs a
bounded, filtered history read, enforces the row limit and projects display rows
with a generation timestamp. `HistoryExportRenderer` consumes that prepared
report to produce CSV, Excel-compatible HTML or a streamed PDF without querying
the database. CSV formula protection, text escaping, column order and filenames
remain part of the renderer's contract.

`TrendExportService` retrieves the existing trend report, validates its period
and point limit, and records export timing. `TrendPdfRenderer` owns PDF layout.
Both PDF paths check cancellation before and after generation and keep
`ExportFile` streaming; they do not allocate a complete PDF byte array in the
request service. Renderers are stateless singleton services; database readers
remain scoped.
