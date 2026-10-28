using WT.Blazor.Components.Pages;
using WT.Blazor.Models;

namespace WT.Blazor.Components.Administration;

// A page can retain a quick-edit session while filters temporarily unmount its row.
// Persistence and clinical rules remain in the management service.
public sealed class WaitingTimeEditorSession
{
    internal WaitingTimeFormState Form { get; } = new();
    internal WaitingTimeInput Previous { get; set; } = new();
    internal WaitingTimeInput? Acknowledged { get; set; }
    internal (int Id, int Version)? Loaded { get; set; }
    internal string? Error { get; set; }
    internal bool Conflict { get; set; }
    internal bool Busy { get; set; }
    internal bool ShowPreview { get; set; }
    internal bool ConfirmDiscard { get; set; }
}
