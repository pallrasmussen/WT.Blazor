using Microsoft.AspNetCore.Components;
using WT.Blazor.Components.Administration;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using WT.Blazor.Models;
using WT.Blazor.Services;
using WT.Blazor.Localization;

namespace WT.Blazor.Components.Pages;

public partial class Internal
{
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }
    [Inject] private Microsoft.Extensions.Options.IOptions<AdGroupsOptions> AdGroupsOptions { get; set; } = default!;
    [Inject] private PersistentComponentState ApplicationState { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "new")] public bool? OpenCreateForm { get; set; }
    [SupplyParameterFromQuery(Name = "preset")] public string? QueryPreset { get; set; }
    [SupplyParameterFromQuery(Name = "hospital")] public string? QueryHospital { get; set; }
    [SupplyParameterFromQuery(Name = "department")] public string? QueryDepartment { get; set; }
    [SupplyParameterFromQuery(Name = "review")] public bool? QueryNeedsReview { get; set; }

    private string AdminGroup => AdGroupsOptions.Value.Admin;

    private bool isSuperAdmin;
    private string username = "";
    private PersistingComponentStateSubscription? persistenceSubscription;

    private const string AllPreset = "all";
    private const string ReviewPreset = "review";
    private const string SharedHospitalPreset = "shared";
    private const string NationalHospitalPreset = "national";
    private const string KlaksvikPreset = "klaksvik";
    private const string SuduroyPreset = "suduroy";
    private string? selectedPreset;
    private bool isLoading;
    private List<WaitingTime> items = [];
    private Dictionary<int, ReviewInfo> latestReviews = [];
    private PortalSettings portalSettings = new();
    private DateTime CurrentUtc => TimeProvider.GetUtcNow().UtcDateTime;
    private EstimateReviewReason ReviewReasons(WaitingTime item) => EstimateReview.Reasons(item, Math.Min(portalSettings.GreenMaximumAgeMonths, portalSettings.EstimateExpiryMonths), CurrentUtc);
    private List<WaitingTime> visibleItems => items
        .Where(item => filters.Inactive ? !item.IsActive : item.IsActive)
        .Where(item => !filters.NeedsReview || ReviewReasons(item) != EstimateReviewReason.None)
        .Where(item => !filters.NeedsReview || filters.Reason == EstimateReviewReason.None || ReviewReasons(item).HasFlag(filters.Reason))
        .Where(item => !filters.NeedsReview || string.IsNullOrWhiteSpace(filters.Owner) || (item.ResponsibleDepartment ?? "").Contains(filters.Owner.Trim(), StringComparison.CurrentCultureIgnoreCase))
        .Where(item => string.IsNullOrWhiteSpace(filters.Search) || item.Name.Contains(filters.Search.Trim(), StringComparison.CurrentCultureIgnoreCase))
        .OrderByDescending(item => filters.NeedsReview ? EstimateReview.Priority(ReviewReasons(item)) : 0)
        .ThenBy(item => filters.NeedsReview ? ReferralEstimate.ReviewDate(item) : DateTime.MinValue)
        .ThenBy(item => item.Name).ThenBy(item => item.Id)
        .ToList();
    private string? statusMessage;
    private WaitingTimeUndo? pendingUndo;
    private CancellationTokenSource? statusCts;

    private readonly AdministrationFilterState filters = new();
    private bool showCreateForm;
    private WaitingTime? editItem;
    private WaitingTime? quickEditItem;
    private WaitingTimeEditorSession quickSession = new();
    private bool confirmReviewAll;
    private bool isReviewing;
    private WaitingTime? itemPendingDeactivation;
    private string? deactivationReason;
    private string? deactivationErrorMessage;

    protected override void OnInitialized()
    {
        persistenceSubscription = ApplicationState.RegisterOnPersisting(PersistAuthenticationState);
        PortalSettingsNotifier.Changed += HandleSettingsChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        portalSettings = await PortalSettingsReader.GetAsync();
        if (ApplicationState.TryTakeFromJson<bool>("Internal.IsSuperAdmin", out var persistedIsSuperAdmin))
        {
            isSuperAdmin = persistedIsSuperAdmin;
            if (ApplicationState.TryTakeFromJson<string>("Internal.Username", out var persistedUsername))
            {
                username = persistedUsername ?? "";
            }
        }
        else if (AuthState is not null)
        {
            var state = await AuthState;
            isSuperAdmin = Policies.IsSuperAdmin(state.User, AdminGroup);
            username = state.User.Identity?.Name ?? "";
        }
        filters.Hospital = DisplayNames.TryHospitalFrom(QueryHospital);
        filters.Department = DisplayNames.TryDepartmentFrom(QueryDepartment);
        filters.NeedsReview = QueryNeedsReview == true;
        if (IsPreset(QueryPreset))
        {
            selectedPreset = QueryPreset;
            ApplyPreset(selectedPreset!);
        }
        showCreateForm = OpenCreateForm == true && isSuperAdmin;
        await LoadAsync();
    }

    private Task PersistAuthenticationState()
    {
        ApplicationState.PersistAsJson("Internal.IsSuperAdmin", isSuperAdmin);
        ApplicationState.PersistAsJson("Internal.Username", username);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        persistenceSubscription?.Dispose();
        PortalSettingsNotifier.Changed -= HandleSettingsChanged;
        statusCts?.Cancel();
        statusCts?.Dispose();
    }

    private void HandleSettingsChanged(object? sender, EventArgs args) => _ = InvokeAsync(async () =>
    {
        portalSettings = await PortalSettingsReader.GetAsync();
        StateHasChanged();
    });

    private async Task LoadAsync()
    {
        if (filters.Hospital != Hospital.Landssjukrahusid) filters.Department = null;

        isLoading = true;
        StateHasChanged();

        items = isSuperAdmin
            ? await WaitingTimeCatalog.GetAllAsync(filters.Hospital, filters.Department)
            : await WaitingTimeCatalog.GetActiveAsync(filters.Hospital, filters.Department);
        latestReviews = await HistoryReader.GetLatestReviewsAsync(items.Select(item => item.Id));
        isLoading = false;
    }

    private async Task FilterChangedAsync()
    {
        if (filters.NeedsReview) filters.Inactive = false;
        selectedPreset = null;
        UpdateFilterUrl();
        await LoadAsync();
    }

    private async Task ApplyPresetAsync(string preset)
    {
        selectedPreset = preset;
        ApplyPreset(preset);
        UpdateFilterUrl();
        await LoadAsync();
    }

    private void ApplyPreset(string preset)
    {
        filters.Department = null;
        filters.NeedsReview = false;
        filters.Hospital = preset switch
        {
            SharedHospitalPreset => Hospital.Sjukrahusverkid,
            NationalHospitalPreset => Hospital.Landssjukrahusid,
            KlaksvikPreset => Hospital.Klaksvikar,
            SuduroyPreset => Hospital.Suduroyar,
            _ => null
        };
        filters.NeedsReview = preset == ReviewPreset;
        if (filters.NeedsReview) filters.Inactive = false;
    }

    private void UpdateFilterUrl() => NavigationManager.NavigateTo(
        NavigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["preset"] = selectedPreset,
            ["hospital"] = filters.Hospital is null ? null : DisplayNames.For(filters.Hospital.Value),
            ["department"] = filters.Department is null ? null : DisplayNames.For(filters.Department.Value),
            ["review"] = filters.NeedsReview ? true : null
        }), replace: true);

    private static bool IsPreset(string? preset) => preset is AllPreset or ReviewPreset or SharedHospitalPreset or NationalHospitalPreset or KlaksvikPreset or SuduroyPreset;

    private void ShowStatus(string message, WaitingTimeUndo? undo = null)
    {
        statusCts?.Cancel();
        statusCts?.Dispose();
        statusCts = new CancellationTokenSource();
        statusMessage = message;
        pendingUndo = undo;
        _ = DismissStatusAfterDelay(statusCts.Token);
    }

    private async Task DismissStatusAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(4000, token);
            statusMessage = null;
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException) { }
    }

    private void HandleCreated(WaitingTime created)
    {
        ShowStatus(string.Format(AppText.Get("CreatedMessage"), created.Name));
        showCreateForm = false;
        if (MatchesFilter(created)) ReplaceSaved(created);
    }

    private void HandleSaved(WaitingTimeEditResult result)
    {
        ReplaceSaved(result.Saved);
        ShowStatus(string.Format(AppText.Get("UpdatedMessage"), result.Saved.Name),
            new(result.Saved.Id, result.Saved.Version, result.Previous, false));
        CloseEdit();
        CloseQuickEdit();
    }

    private void ReplaceSaved(WaitingTime saved)
    {
        var index = items.FindIndex(item => item.Id == saved.Id);
        if (index >= 0) items[index] = saved;
        else items.Add(saved);
        items = items.OrderBy(item => item.Name).ToList();
    }

    private bool MatchesFilter(WaitingTime item) => (filters.Hospital is null || item.Hospital == filters.Hospital)
        && (filters.Department is null || item.Department == filters.Department);

    private void OpenEdit(WaitingTime item) { CloseQuickEdit(); editItem = item; }
    private void OpenQuickEdit(WaitingTime item) { CloseEdit(); quickSession = new(); quickEditItem = item; }
    private void CloseEdit() => editItem = null;
    private void CloseQuickEdit() { quickEditItem = null; quickSession = new(); }
    protected override void OnParametersSet() => showCreateForm = OpenCreateForm == true && isSuperAdmin;
    private void CloseCreate()
    {
        showCreateForm = false;
        NavigationManager.NavigateTo("/internal", replace: true);
    }

    private async Task ReloadConflictedItemAsync(bool quick)
    {
        var id = (quick ? quickEditItem : editItem)?.Id;
        await LoadAsync();
        var latest = items.FirstOrDefault(item => item.Id == id && item.IsActive);
        if (latest is null)
        {
            CloseEdit();
            CloseQuickEdit();
            ShowStatus(AppText.Get("WaitingTimeNotFound"));
            return;
        }
        if (quick) OpenQuickEdit(latest);
        else OpenEdit(latest);
    }

    private void BeginReviewAll() => confirmReviewAll = true;
    private void CancelReviewAll() => confirmReviewAll = false;
    private async Task ConfirmReviewAllAsync()
    {
        var reviewItems = visibleItems.ToList();
        isReviewing = true;
        try
        {
            var reviewed = 0;
            foreach (var item in reviewItems)
                if (await MarkReviewedAsync(item, showStatus: false)) reviewed++;
            ShowStatus(string.Format(AppText.Get("ReviewedCountMessage"), reviewed));
            confirmReviewAll = false;
        }
        finally { isReviewing = false; }
    }

    private async Task<bool> MarkReviewedAsync(WaitingTime item, bool showStatus = true)
    {
        try
        {
            var saved = await WaitingTimeManager.MarkReviewedAsync(item.Id, item.Version, username);
            ReplaceSaved(saved);
            latestReviews[item.Id] = new ReviewInfo(saved.UpdatedAt, username);
            if (showStatus) ShowStatus(string.Format(AppText.Get("ReviewedMessage"), saved.Name));
            return true;
        }
        catch (WaitingTimeConcurrencyException) { await LoadAsync(); ShowStatus(AppText.Get("WaitingTimeConflict")); }
        catch (WaitingTimeNotFoundException) { items.Remove(item); ShowStatus(AppText.Get("WaitingTimeNotFound")); }
        return false;
    }

    private void HandleReviewQueueKeyDown(KeyboardEventArgs args)
    {
        if (!filters.NeedsReview || args.Key is not ("ArrowDown" or "ArrowUp")) return;
        var queue = visibleItems;
        if (queue.Count == 0) return;
        var currentIndex = quickEditItem is null ? -1 : queue.FindIndex(item => item.Id == quickEditItem.Id);
        var nextIndex = args.Key == "ArrowDown" ? Math.Min(currentIndex + 1, queue.Count - 1) : Math.Max(currentIndex - 1, 0);
        OpenQuickEdit(queue[nextIndex]);
    }

    private void BeginDeactivation(WaitingTime item)
    {
        itemPendingDeactivation = item;
        deactivationReason = null;
        deactivationErrorMessage = null;
    }
    private void CancelDeactivation() => itemPendingDeactivation = null;
    private async Task ConfirmDeactivationAsync()
    {
        if (itemPendingDeactivation is null) return;
        var previous = WaitingTimeInput.From(itemPendingDeactivation);
        try
        {
            var saved = await WaitingTimeManager.DeactivateAsync(itemPendingDeactivation.Id, itemPendingDeactivation.Version, username, deactivationReason);
            ReplaceSaved(saved);
            ShowStatus(string.Format(AppText.Get("DeactivatedMessage"), saved.Name), new(saved.Id, saved.Version, previous, true));
            CancelDeactivation();
        }
        catch (WaitingTimeConcurrencyException) { deactivationErrorMessage = AppText.Get("WaitingTimeConflict"); }
        catch (WaitingTimeNotFoundException) { deactivationErrorMessage = AppText.Get("WaitingTimeNotFound"); }
    }

    private async Task ActivateAsync(WaitingTime item)
    {
        try
        {
            var saved = await WaitingTimeManager.ReactivateAsync(item.Id, item.Version, username);
            ReplaceSaved(saved);
            ShowStatus(string.Format(AppText.Get("ReactivatedMessage"), saved.Name));
        }
        catch (WaitingTimeConcurrencyException) { await LoadAsync(); ShowStatus(AppText.Get("WaitingTimeConflict")); }
        catch (WaitingTimeNotFoundException) { items.Remove(item); ShowStatus(AppText.Get("WaitingTimeNotFound")); }
    }

    private async Task UndoAsync()
    {
        var undo = pendingUndo;
        if (undo is null) return;
        pendingUndo = null;
        try
        {
            var restored = undo.Reactivate
                ? await WaitingTimeManager.ReactivateAsync(undo.Id, undo.ExpectedVersion, username)
                : await WaitingTimeManager.UpdateAsync(new WaitingTimeUpdate(undo.Previous)
                {
                    Id = undo.Id,
                    ExpectedVersion = undo.ExpectedVersion,
                    Username = username,
                    ConfirmLargeChange = true
                });
            if (items.Any(item => item.Id == restored.Id) || MatchesFilter(restored)) ReplaceSaved(restored);
            ShowStatus(AppText.Get("UndoCompleted"));
        }
        catch (WaitingTimeConcurrencyException) { await LoadAsync(); ShowStatus(AppText.Get("WaitingTimeConflict")); }
        catch (WaitingTimeNotFoundException) { await LoadAsync(); ShowStatus(AppText.Get("WaitingTimeNotFound")); }
    }

    private sealed record WaitingTimeUndo(int Id, int ExpectedVersion, WaitingTimeInput Previous, bool Reactivate);
}
