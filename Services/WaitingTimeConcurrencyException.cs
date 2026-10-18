namespace WT.Blazor.Services;

public sealed class WaitingTimeConcurrencyException : InvalidOperationException
{
    public WaitingTimeConcurrencyException() : base("The waiting time was changed by another user.")
    {
    }
}