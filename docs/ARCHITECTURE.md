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
