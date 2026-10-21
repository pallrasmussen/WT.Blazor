using System.Runtime.CompilerServices;

namespace WT.Blazor.Data;

internal sealed class DatabaseOperation : IDisposable
{
    private static readonly ConditionalWeakTable<ApplicationDbContext, SemaphoreSlim> Gates = new();
    private readonly SemaphoreSlim gate;

    private DatabaseOperation(SemaphoreSlim gate) => this.gate = gate;

    public static async Task<DatabaseOperation> EnterAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var gate = Gates.GetValue(db, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new DatabaseOperation(gate);
    }

    public void Dispose() => gate.Release();
}
