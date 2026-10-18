namespace WT.Blazor.Models;

public class WaitingTimeHistoryEntry
{
    public int Id { get; set; }

    public int WaitingTimeId { get; set; }
    public WaitingTime? WaitingTime { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string RequestId { get; set; } = string.Empty;

    // Snapshot fields so history remains accurate even if the waiting time is later edited.
    public Hospital Hospital { get; set; }
    public Department? Department { get; set; }
    public string Name { get; set; } = string.Empty;

    public HistoryAction Action { get; set; }
    public string? Detail { get; set; }

    public int? OldWaitValue { get; set; }
    public WaitUnit? OldUnit { get; set; }
    public int? OldWaitDays { get; set; }
    public int? NewWaitValue { get; set; }
    public WaitUnit? NewUnit { get; set; }
    public int? NewWaitDays { get; set; }

    public string? Username { get; set; }
}
