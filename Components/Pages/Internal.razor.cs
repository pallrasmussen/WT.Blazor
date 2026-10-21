using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using WT.Blazor.Models;
using WT.Blazor.Services;
using WT.Blazor.Localization;

namespace WT.Blazor.Components.Pages;

public partial class Internal
{
    private static readonly string LandssjukrahusidName = DisplayNames.For(Hospital.Landssjukrahusid);
    private static readonly Hospital[] FilterHospitals = Enum.GetValues<Hospital>();
    private static readonly Hospital[] EditableHospitals = FilterHospitals
        .Where(hospital => hospital != Hospital.Sjukrahusverkid)
        .ToArray();
    private static readonly Department[] AllDepartments = Enum.GetValues<Department>();
    private static readonly string[] FormHospitals = EditableHospitals.Select(DisplayNames.For).ToArray();
    private static readonly string[] FormDepartments = AllDepartments.Select(DisplayNames.For).ToArray();

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

    private Hospital? filterHospital;
    private Department? filterDepartment;
    private string searchText = "";
    private bool showNeedsReview;
    private bool showInactive;
    private const string AllPreset = "all";
    private const string ReviewPreset = "review";
    private const string SharedHospitalPreset = "shared";
    private const string NationalHospitalPreset = "national";
    private const string KlaksvikPreset = "klaksvik";
    private const string SuduroyPreset = "suduroy";
    private string? selectedPreset;
    private bool isLoading;
    private bool isSaving;
    private List<WaitingTime> items = [];
    private Dictionary<int, ReviewInfo> latestReviews = [];
    private PortalSettings portalSettings = new();
    private DateTime CurrentUtc => TimeProvider.GetUtcNow().UtcDateTime;
    private List<WaitingTime> visibleItems => items
        .Where(item => showInactive ? !item.IsActive : item.IsActive)
        .Where(item => !showNeedsReview || UpdatedStatus.RequiresReview(item.UpdatedAt, portalSettings.GreenMaximumAgeMonths, CurrentUtc))
        .Where(item => string.IsNullOrWhiteSpace(searchText) || item.Name.Contains(searchText.Trim(), StringComparison.CurrentCultureIgnoreCase))
        .ToList();
    private string? statusMessage;
    private WaitingTimeUndo? pendingUndo;
    private string? createErrorMessage;
    private CancellationTokenSource? statusCts;

    private readonly WaitingTimeFormState createForm = new();
    private bool showCreateForm;

    private WaitingTime? editItem;
    private readonly WaitingTimeFormState editForm = new();
    private string? editErrorMessage;
    private bool editConflict;
    private bool quickEditConflict;
    private ElementReference editModalRef;
    private ElementReference editErrorRef;
    private bool shouldFocusEditModal;
    private bool shouldFocusEditError;
    private WaitingTime? quickEditItem;
    private readonly WaitingTimeFormState quickEditForm = new();
    private string? quickEditErrorMessage;
    private bool isQuickSaving;
    private bool showQuickEditPreview;
    private bool confirmDiscardQuickEdit;
    private bool confirmDiscardEdit;
    private bool showEditPreview;
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
        filterHospital = DisplayNames.TryHospitalFrom(QueryHospital);
        filterDepartment = DisplayNames.TryDepartmentFrom(QueryDepartment);
        showNeedsReview = QueryNeedsReview == true;
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
        if (filterHospital != Hospital.Landssjukrahusid) filterDepartment = null;

        isLoading = true;
        StateHasChanged();

        items = isSuperAdmin
            ? await WaitingTimeCatalog.GetAllAsync(filterHospital, filterDepartment)
            : await WaitingTimeCatalog.GetActiveAsync(filterHospital, filterDepartment);
        latestReviews = await HistoryReader.GetLatestReviewsAsync(items.Select(item => item.Id));
        isLoading = false;
    }

    private async Task FilterChangedAsync()
    {
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
        filterDepartment = null;
        showNeedsReview = false;
        filterHospital = preset switch
        {
            SharedHospitalPreset => Hospital.Sjukrahusverkid,
            NationalHospitalPreset => Hospital.Landssjukrahusid,
            KlaksvikPreset => Hospital.Klaksvikar,
            SuduroyPreset => Hospital.Suduroyar,
            _ => null
        };
        showNeedsReview = preset == ReviewPreset;
    }

    private void UpdateFilterUrl() => NavigationManager.NavigateTo(
        NavigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["preset"] = selectedPreset,
            ["hospital"] = filterHospital is null ? null : DisplayNames.For(filterHospital.Value),
            ["department"] = filterDepartment is null ? null : DisplayNames.For(filterDepartment.Value),
            ["review"] = showNeedsReview ? true : null
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

    private async Task AddNew()
    {
        if (string.IsNullOrEmpty(createForm.HospitalName)) { createErrorMessage = AppText.Get("SelectHospitalValidation"); return; }
        if (createForm.HospitalName == LandssjukrahusidName && string.IsNullOrEmpty(createForm.DepartmentName)) { createErrorMessage = AppText.Get("SelectDepartmentValidation"); return; }
        if (string.IsNullOrWhiteSpace(createForm.Name)) { createErrorMessage = AppText.Get("EnterNameValidation"); return; }
        if (WaitValidation(createForm) is { } waitError) { createErrorMessage = waitError; return; }

        createErrorMessage = null;
        isSaving = true;
        StateHasChanged();

        var hospital = DisplayNames.HospitalFrom(createForm.HospitalName);
        Department? department = hospital == Hospital.Landssjukrahusid && !string.IsNullOrEmpty(createForm.DepartmentName)
            ? DisplayNames.DepartmentFrom(createForm.DepartmentName) : null;

        var created = await WaitingTimeManager.CreateAsync(new WaitingTimeCreate(
            createForm.Name.Trim(),
            hospital,
            department,
            createForm.WaitValue,
            createForm.Unit,
            createForm.Note?.Trim(),
            username));
        ShowStatus(string.Format(AppText.Get("CreatedMessage"), createForm.Name));
        createForm.Clear();
        showCreateForm = false;
        isSaving = false;

        // Only add it to the visible list locally if it matches the current filter -
        // avoids a full reload that would discard unsaved edits in other rows.
        var matchesFilter = (filterHospital is null || created.Hospital == filterHospital)
            && (filterDepartment is null || created.Department == filterDepartment);
        if (matchesFilter)
        {
            items.Add(created);
            items = items.OrderBy(i => i.Name).ToList();
        }
    }

    private void OpenEdit(WaitingTime item)
    {
        CloseQuickEdit();
        editItem = item;
        editForm.Load(item);
        editErrorMessage = null;
        editConflict = false;
        showEditPreview = false;
        confirmDiscardEdit = false;
        shouldFocusEditModal = true;
    }

    protected override void OnParametersSet()
    {
        showCreateForm = OpenCreateForm == true && isSuperAdmin;
    }

    private void CloseCreate()
    {
        showCreateForm = false;
        NavigationManager.NavigateTo("/internal", replace: true);
    }

    private void CloseEdit()
    {
        editItem = null;
        confirmDiscardEdit = false;
        showEditPreview = false;
    }

    private void RequestCloseEdit()
    {
        confirmDiscardEdit = EditFormHasChanges();
        if (!confirmDiscardEdit) CloseEdit();
    }

    private void ConfirmDiscardEdit() => CloseEdit();

    private void CancelDiscardEdit() => confirmDiscardEdit = false;

    private void OpenQuickEdit(WaitingTime item)
    {
        CloseEdit();
        quickEditItem = item;
        quickEditForm.Load(item);
        quickEditErrorMessage = null;
        quickEditConflict = false;
        showQuickEditPreview = false;
        confirmDiscardQuickEdit = false;
    }

    private void CloseQuickEdit()
    {
        quickEditItem = null;
        quickEditErrorMessage = null;
        quickEditConflict = false;
        confirmDiscardQuickEdit = false;
        showQuickEditPreview = false;
    }

    private void RequestCloseQuickEdit()
    {
        confirmDiscardQuickEdit = QuickEditFormHasChanges();
        if (!confirmDiscardQuickEdit) CloseQuickEdit();
    }

    private void ConfirmDiscardQuickEdit() => CloseQuickEdit();

    private void CancelDiscardQuickEdit() => confirmDiscardQuickEdit = false;

    private bool EditFormHasChanges() => editItem is not null && (
        editForm.Name != editItem.Name
        || editForm.HospitalName != DisplayNames.For(editItem.Hospital)
        || editForm.DepartmentName != (editItem.Department is null ? "" : DisplayNames.For(editItem.Department.Value))
        || editForm.WaitValue != editItem.WaitValue
        || editForm.Unit != editItem.Unit
        || (editForm.Note ?? "") != (editItem.Note ?? ""));

    private bool QuickEditFormHasChanges() => quickEditItem is not null && (
        quickEditForm.WaitValue != quickEditItem.WaitValue
        || quickEditForm.Unit != quickEditItem.Unit
        || (quickEditForm.Note ?? "") != (quickEditItem.Note ?? ""));

    private void BeginEditPreview()
    {
        if (editItem is null) return;
        if (WaitValidation(editForm) is { } waitError) { SetEditError(waitError); return; }
        if (isSuperAdmin && string.IsNullOrWhiteSpace(editForm.Name)) { SetEditError(AppText.Get("EnterNameValidation")); return; }
        if (!EditFormHasChanges()) { SetEditError(AppText.Get("NoChanges")); return; }
        showEditPreview = true;
    }

    private void CancelEditPreview() => showEditPreview = false;

    private void BeginQuickEditPreview()
    {
        if (quickEditItem is null) return;
        if (WaitValidation(quickEditForm) is { } waitError) { quickEditErrorMessage = waitError; return; }
        if (!QuickEditFormHasChanges()) { quickEditErrorMessage = AppText.Get("NoChanges"); return; }
        showQuickEditPreview = true;
    }

    private void CancelQuickEditPreview() => showQuickEditPreview = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (shouldFocusEditModal)
        {
            shouldFocusEditModal = false;
            try
            {
                await editModalRef.FocusAsync();
            }
            catch
            {
                // Focusing is a nice-to-have for keyboard/Escape support; if the browser-side
                // element isn't ready yet, just skip it rather than crashing the whole circuit.
            }
        }

        if (shouldFocusEditError)
        {
            shouldFocusEditError = false;
            await editErrorRef.FocusAsync();
        }

    }

    private async Task SaveEdit()
    {
        if (editItem is null) return;
        if (WaitValidation(editForm) is { } waitError) { SetEditError(waitError); return; }

        var hospital = editItem.Hospital;
        var department = editItem.Department;
        if (isSuperAdmin)
        {
            if (string.IsNullOrWhiteSpace(editForm.Name)) { SetEditError(AppText.Get("EnterNameValidation")); return; }

            hospital = DisplayNames.HospitalFrom(editForm.HospitalName);
            if (hospital == Hospital.Landssjukrahusid && string.IsNullOrEmpty(editForm.DepartmentName)) { SetEditError(AppText.Get("SelectDepartmentValidation")); return; }

            department = hospital == Hospital.Landssjukrahusid && !string.IsNullOrEmpty(editForm.DepartmentName)
                ? DisplayNames.DepartmentFrom(editForm.DepartmentName) : null;
        }

        var previous = WaitingTimeSnapshot.From(editItem);
        try
        {
            await WaitingTimeManager.UpdateAsync(new WaitingTimeUpdate(
                editItem.Id,
                editItem.Version,
                isSuperAdmin ? editForm.Name.Trim() : editItem.Name,
                hospital,
                department,
                editForm.WaitValue,
                editForm.Unit,
                editForm.Note,
                username));
        }
        catch (WaitingTimeConcurrencyException)
        {
            editConflict = true;
            SetEditError(AppText.Get("WaitingTimeConflict"));
            return;
        }
        catch (WaitingTimeNotFoundException)
        {
            SetEditError(AppText.Get("WaitingTimeNotFound"));
            return;
        }
        var item = items.FirstOrDefault(i => i.Id == editItem.Id);
        if (item is not null)
        {
            if (isSuperAdmin) item.Name = editForm.Name.Trim();
            item.Hospital = hospital;
            item.Department = department;
            item.WaitValue = editForm.WaitValue;
            item.Unit = editForm.Unit;
            item.Note = editForm.Note?.Trim();
            item.UpdatedAt = TimeProvider.GetUtcNow().UtcDateTime;
            item.Version++;
            items = items.OrderBy(i => i.Name).ToList();
        }
        ShowStatus(string.Format(AppText.Get("UpdatedMessage"), editForm.Name), new WaitingTimeUndo(editItem.Id, (item ?? editItem).Version, previous, false));
        CloseEdit();
    }

    private async Task SaveQuickEdit()
    {
        if (quickEditItem is null) return;
        if (WaitValidation(quickEditForm) is { } waitError)
        {
            quickEditErrorMessage = waitError;
            return;
        }

        var previous = WaitingTimeSnapshot.From(quickEditItem);
        isQuickSaving = true;
        quickEditErrorMessage = null;
        quickEditConflict = false;
        try
        {
            await WaitingTimeManager.UpdateAsync(new WaitingTimeUpdate(
                quickEditItem.Id,
                quickEditItem.Version,
                quickEditItem.Name,
                quickEditItem.Hospital,
                quickEditItem.Department,
                quickEditForm.WaitValue,
                quickEditForm.Unit,
                quickEditForm.Note?.Trim(),
                username));

            quickEditItem.WaitValue = quickEditForm.WaitValue;
            quickEditItem.Unit = quickEditForm.Unit;
            quickEditItem.Note = quickEditForm.Note?.Trim();
            quickEditItem.UpdatedAt = TimeProvider.GetUtcNow().UtcDateTime;
            quickEditItem.Version++;
            ShowStatus(string.Format(AppText.Get("UpdatedMessage"), quickEditItem.Name), new WaitingTimeUndo(quickEditItem.Id, quickEditItem.Version, previous, false));
            CloseQuickEdit();
        }
        catch (WaitingTimeConcurrencyException)
        {
            quickEditConflict = true;
            quickEditErrorMessage = AppText.Get("WaitingTimeConflict");
        }
        catch (WaitingTimeNotFoundException)
        {
            quickEditErrorMessage = AppText.Get("WaitingTimeNotFound");
        }
        finally
        {
            isQuickSaving = false;
        }
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

    private void SetEditError(string message)
    {
        editErrorMessage = message;
        shouldFocusEditError = true;
    }

    private IEnumerable<string> EditPreviewChanges()
    {
        if (editItem is null) yield break;
        if (editForm.WaitValue != editItem.WaitValue || editForm.Unit != editItem.Unit)
            yield return $"{AppText.Get("Wait")}: {WaitText(editItem.WaitValue, editItem.Unit)} -> {WaitText(editForm.WaitValue, editForm.Unit)}";
        if (isSuperAdmin && editForm.Name != editItem.Name)
            yield return $"{AppText.Get("SpecialtyTreatment")}: {editItem.Name} -> {editForm.Name}";
        if (isSuperAdmin && editForm.HospitalName != DisplayNames.For(editItem.Hospital))
            yield return string.Format(AppText.Get("ChangeHospital"), DisplayNames.For(editItem.Hospital), editForm.HospitalName);
        if (isSuperAdmin && editForm.DepartmentName != (editItem.Department is null ? "" : DisplayNames.For(editItem.Department.Value)))
            yield return $"Depil: {(editItem.Department is null ? "-" : DisplayNames.For(editItem.Department.Value))} -> {editForm.DepartmentName}";
        if ((editForm.Note ?? "") != (editItem.Note ?? ""))
            yield return $"{AppText.Get("Note")}: {(string.IsNullOrWhiteSpace(editItem.Note) ? "-" : editItem.Note)} -> {(string.IsNullOrWhiteSpace(editForm.Note) ? "-" : editForm.Note)}";
    }

    private IEnumerable<string> QuickEditPreviewChanges()
    {
        if (quickEditItem is null) yield break;
        if (quickEditForm.WaitValue != quickEditItem.WaitValue || quickEditForm.Unit != quickEditItem.Unit)
            yield return $"{AppText.Get("Wait")}: {WaitText(quickEditItem.WaitValue, quickEditItem.Unit)} -> {WaitText(quickEditForm.WaitValue, quickEditForm.Unit)}";
        if ((quickEditForm.Note ?? "") != (quickEditItem.Note ?? ""))
            yield return $"{AppText.Get("Note")}: {(string.IsNullOrWhiteSpace(quickEditItem.Note) ? "-" : quickEditItem.Note)} -> {(string.IsNullOrWhiteSpace(quickEditForm.Note) ? "-" : quickEditForm.Note)}";
    }

    private static string? WaitValidation(WaitingTimeFormState form) => form.WaitValue < 0
        ? AppText.Get("NegativeWaitValidation")
        : form.WaitValue > WaitDuration.MaximumValue(form.Unit)
            ? string.Format(AppText.Get("MaximumWaitValidation"), WaitDuration.MaximumValue(form.Unit))
            : null;

    private static string WaitText(int value, WaitUnit unit) => $"{value} {DisplayNames.For(unit, value)}";

    private void BeginReviewAll() => confirmReviewAll = true;

    private void CancelReviewAll() => confirmReviewAll = false;

    private async Task ConfirmReviewAllAsync()
    {
        var reviewItems = visibleItems.ToList();
        isReviewing = true;
        try
        {
            foreach (var item in reviewItems)
                await MarkReviewedAsync(item, showStatus: false);
            ShowStatus(string.Format(AppText.Get("ReviewedCountMessage"), reviewItems.Count));
            confirmReviewAll = false;
        }
        finally
        {
            isReviewing = false;
        }
    }

    private async Task MarkReviewedAsync(WaitingTime item, bool showStatus = true)
    {
        try
        {
            await WaitingTimeManager.MarkReviewedAsync(item.Id, item.Version, username);
            item.UpdatedAt = TimeProvider.GetUtcNow().UtcDateTime;
            item.Version++;
            latestReviews[item.Id] = new ReviewInfo(item.UpdatedAt, username);
            if (showStatus) ShowStatus(string.Format(AppText.Get("ReviewedMessage"), item.Name));
        }
        catch (WaitingTimeConcurrencyException)
        {
            await LoadAsync();
            ShowStatus(AppText.Get("WaitingTimeConflict"));
        }
        catch (WaitingTimeNotFoundException)
        {
            items.Remove(item);
            ShowStatus(AppText.Get("WaitingTimeNotFound"));
        }
    }

    private void HandleReviewQueueKeyDown(KeyboardEventArgs args)
    {
        if (!showNeedsReview || args.Key is not ("ArrowDown" or "ArrowUp")) return;

        var queue = visibleItems;
        if (queue.Count == 0) return;
        var currentIndex = quickEditItem is null ? -1 : queue.FindIndex(item => item.Id == quickEditItem.Id);
        var nextIndex = args.Key == "ArrowDown"
            ? Math.Min(currentIndex + 1, queue.Count - 1)
            : Math.Max(currentIndex - 1, 0);
        OpenQuickEdit(queue[nextIndex]);
    }

    private void BeginDeactivation(WaitingTime item)
    {
        itemPendingDeactivation = item;
        deactivationReason = null;
        deactivationErrorMessage = null;
    }

    private void CancelDeactivation()
    {
        itemPendingDeactivation = null;
    }

    private async Task ConfirmDeactivationAsync()
    {
        if (itemPendingDeactivation is null) return;

        var previous = WaitingTimeSnapshot.From(itemPendingDeactivation);
        try
        {
            await WaitingTimeManager.DeactivateAsync(itemPendingDeactivation.Id, itemPendingDeactivation.Version, username, deactivationReason);
        }
        catch (WaitingTimeConcurrencyException)
        {
            deactivationErrorMessage = AppText.Get("WaitingTimeConflict");
            return;
        }
        catch (WaitingTimeNotFoundException)
        {
            deactivationErrorMessage = AppText.Get("WaitingTimeNotFound");
            return;
        }
        itemPendingDeactivation.IsActive = false;
        itemPendingDeactivation.Version++;
        ShowStatus(string.Format(AppText.Get("DeactivatedMessage"), itemPendingDeactivation.Name), new WaitingTimeUndo(itemPendingDeactivation.Id, itemPendingDeactivation.Version, previous, true));
        CancelDeactivation();
    }

    private async Task ActivateAsync(WaitingTime item)
    {
        try
        {
            var activated = await WaitingTimeManager.ReactivateAsync(item.Id, item.Version, username);
            var index = items.FindIndex(candidate => candidate.Id == item.Id);
            if (index >= 0) items[index] = activated;
            ShowStatus(string.Format(AppText.Get("ReactivatedMessage"), activated.Name));
        }
        catch (WaitingTimeConcurrencyException)
        {
            await LoadAsync();
            ShowStatus(AppText.Get("WaitingTimeConflict"));
        }
        catch (WaitingTimeNotFoundException)
        {
            items.Remove(item);
            ShowStatus(AppText.Get("WaitingTimeNotFound"));
        }
    }

    private async Task UndoAsync()
    {
        var undo = pendingUndo;
        if (undo is null) return;

        pendingUndo = null;
        try
        {
            if (undo.Reactivate)
            {
                var restored = await WaitingTimeManager.ReactivateAsync(undo.Id, undo.ExpectedVersion, username);
                var existingIndex = items.FindIndex(item => item.Id == restored.Id);
                if (existingIndex >= 0)
                {
                    items[existingIndex] = restored;
                }
                else if ((filterHospital is null || restored.Hospital == filterHospital)
                    && (filterDepartment is null || restored.Department == filterDepartment))
                {
                    items.Add(restored);
                    items = items.OrderBy(item => item.Name).ToList();
                }
            }
            else
            {
                await WaitingTimeManager.UpdateAsync(new WaitingTimeUpdate(
                    undo.Id, undo.ExpectedVersion, undo.Snapshot.Name, undo.Snapshot.Hospital,
                    undo.Snapshot.Department, undo.Snapshot.WaitValue, undo.Snapshot.Unit, undo.Snapshot.Note, username));
                var item = items.FirstOrDefault(candidate => candidate.Id == undo.Id);
                if (item is not null)
                    undo.Snapshot.ApplyTo(item, undo.ExpectedVersion + 1);
            }
            ShowStatus(AppText.Get("UndoCompleted"));
        }
        catch (WaitingTimeConcurrencyException)
        {
            await LoadAsync();
            ShowStatus(AppText.Get("WaitingTimeConflict"));
        }
        catch (WaitingTimeNotFoundException)
        {
            await LoadAsync();
            ShowStatus(AppText.Get("WaitingTimeNotFound"));
        }
    }

    private sealed record WaitingTimeUndo(int Id, int ExpectedVersion, WaitingTimeSnapshot Snapshot, bool Reactivate);

    private sealed record WaitingTimeSnapshot(string Name, Hospital Hospital, Department? Department, int WaitValue, WaitUnit Unit, string? Note)
    {
        public static WaitingTimeSnapshot From(WaitingTime item) => new(item.Name, item.Hospital, item.Department, item.WaitValue, item.Unit, item.Note);

        public void ApplyTo(WaitingTime item, int version)
        {
            item.Name = Name;
            item.Hospital = Hospital;
            item.Department = Department;
            item.WaitValue = WaitValue;
            item.Unit = Unit;
            item.Note = Note;
            item.UpdatedAt = DateTime.UtcNow;
            item.Version = version;
        }
    }

}
