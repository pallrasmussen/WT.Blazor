using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using WT.Blazor.Components.Pages;
using WT.Blazor.Localization;
using WT.Blazor.Models;
using WT.Blazor.Services;

namespace WT.Blazor.Components.Administration;

public sealed record WaitingTimeEditResult(WaitingTime Saved, WaitingTimeInput Previous);

// Full and quick editing share validation, acknowledgement, concurrency and saving.
public abstract class WaitingTimeEditorBase : ComponentBase
{
    [Parameter, EditorRequired] public WaitingTime Item { get; set; } = default!;
    [Parameter] public bool IsSuperAdmin { get; set; }
    [Parameter] public string Username { get; set; } = "";
    [Parameter] public EventCallback<WaitingTimeEditResult> Saved { get; set; }
    [Parameter] public EventCallback Close { get; set; }
    [Parameter] public EventCallback Reload { get; set; }
    [Parameter] public WaitingTimeEditorSession Session { get; set; } = new();
    [Parameter] public EventCallback StateChanged { get; set; }
    [Inject] protected IWaitingTimeManagementService Manager { get; set; } = default!;
    [Inject] private ILogger<WaitingTimeEditorBase> Logger { get; set; } = default!;

    protected abstract bool Quick { get; }
    protected WaitingTimeFormState Form => Session.Form;
    protected string? Error { get => Session.Error; private set => Session.Error = value; }
    protected bool Conflict { get => Session.Conflict; private set => Session.Conflict = value; }
    protected bool Busy { get => Session.Busy; private set => Session.Busy = value; }
    protected bool ShowPreview { get => Session.ShowPreview; private set => Session.ShowPreview = value; }
    protected bool ConfirmDiscard { get => Session.ConfirmDiscard; private set => Session.ConfirmDiscard = value; }
    protected ElementReference DialogElement;
    protected ElementReference ErrorElement;
    protected static Hospital[] EditableHospitals => Enum.GetValues<Hospital>();
    protected static string[] FormDepartments => Enum.GetValues<Department>().Select(DisplayNames.For).ToArray();
    protected static string LandssjukrahusidName => DisplayNames.For(Hospital.Landssjukrahusid);
    private WaitingTimeInput previous { get => Session.Previous; set => Session.Previous = value; }
    private WaitingTimeInput? acknowledged { get => Session.Acknowledged; set => Session.Acknowledged = value; }
    private (int Id, int Version)? loaded { get => Session.Loaded; set => Session.Loaded = value; }
    private bool focusError;

    protected override void OnParametersSet()
    {
        if (loaded == (Item.Id, Item.Version)) return;
        loaded = (Item.Id, Item.Version);
        previous = WaitingTimeInput.From(Item);
        Form.Load(Item);
        Error = null;
        Conflict = false;
        ShowPreview = false;
        ConfirmDiscard = false;
        acknowledged = null;
    }

    protected WaitingTimeInput Input
    {
        get
        {
            var input = Form.ToInput();
            if (Quick) return input with
            {
                Name = previous.Name,
                Hospital = previous.Hospital,
                Department = previous.Department,
                ReferralEndpoint = previous.ReferralEndpoint,
                ResponsibleDepartment = previous.ResponsibleDepartment,
                ComparisonCode = previous.ComparisonCode
            };
            return IsSuperAdmin ? input : input with { Name = previous.Name, Hospital = previous.Hospital, Department = previous.Department };
        }
    }

    protected bool RequiresConfirmation => EstimateChange.RequiresConfirmation(Item, Input.EstimateStatus,
        Input.ReferralEndpoint, Input.WaitValue, Input.Unit, Input.WaitUpperValue);
    protected bool Acknowledged
    {
        get => acknowledged == Input;
        set => acknowledged = value ? Input : null;
    }
    protected IEnumerable<string> PreviewChanges => WaitingTimeEditPreview.Changes(previous, WaitingTimeValidation.Normalize(Input));
    private bool HasChanges => WaitingTimeValidation.Normalize(Input) != WaitingTimeValidation.Normalize(previous);

    protected void BeginPreview()
    {
        if (!Validate()) return;
        if (!HasChanges) { SetError(AppText.Get("NoChanges")); return; }
        acknowledged = null;
        ShowPreview = true;
    }

    protected void CancelPreview() => ShowPreview = false;
    protected void KeepEditing() => ConfirmDiscard = false;
    protected Task DiscardAsync() => Close.InvokeAsync();
    protected Task RequestCloseAsync()
    {
        if (Busy) return Task.CompletedTask;
        ConfirmDiscard = HasChanges;
        return ConfirmDiscard ? Task.CompletedTask : Close.InvokeAsync();
    }
    protected Task HandleKeyDownAsync(KeyboardEventArgs args) => args.Key == "Escape" ? RequestCloseAsync() : Task.CompletedTask;

    protected async Task SaveAsync()
    {
        if (Busy || !Validate()) return;
        if (RequiresConfirmation && !Acknowledged) { ShowPreview = true; return; }
        Busy = true;
        Error = null;
        Conflict = false;
        try
        {
            var saved = await Manager.UpdateAsync(new WaitingTimeUpdate(WaitingTimeValidation.Normalize(Input))
            {
                Id = Item.Id,
                ExpectedVersion = loaded!.Value.Version,
                Username = Username,
                ConfirmLargeChange = Acknowledged
            });
            await Saved.InvokeAsync(new(saved, previous));
        }
        catch (WaitingTimeConcurrencyException)
        {
            Conflict = true;
            SetError(AppText.Get("WaitingTimeConflict"));
        }
        catch (LargeEstimateChangeException)
        {
            acknowledged = null;
            ShowPreview = true;
            SetError(AppText.Get("LargeChangeWarning"));
        }
        catch (WaitingTimeNotFoundException) { SetError(AppText.Get("WaitingTimeNotFound")); }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Could not save waiting time {WaitingTimeId}.", Item.Id);
            SetError(AppText.Get("EstimateSaveFailed"));
        }
        finally
        {
            Busy = false;
            await StateChanged.InvokeAsync();
        }
    }

    private bool Validate()
    {
        if (WaitingTimeValidation.Validate(Input) is not { } issue) return true;
        SetError(WaitingTimeValidationText.Message(issue));
        return false;
    }
    private void SetError(string message) { Error = message; focusError = true; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Quick) return;
        if (firstRender)
        {
            try { await DialogElement.FocusAsync(); }
            catch (InvalidOperationException) { /* Static rendering has no browser focus. */ }
        }
        if (focusError)
        {
            focusError = false;
            await ErrorElement.FocusAsync();
        }
    }
}
