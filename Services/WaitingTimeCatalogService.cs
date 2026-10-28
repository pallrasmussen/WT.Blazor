using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed class WaitingTimeCatalogService(ApplicationDbContext db) : IWaitingTimeCatalogService
{
    public async Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var query = db.WaitingTimes.AsNoTracking().Where(waitingTime => waitingTime.IsActive);
        if (hospital is not null) query = query.Where(waitingTime => waitingTime.Hospital == hospital);
        if (department is not null) query = query.Where(waitingTime => waitingTime.Department == department);
        return await query.OrderBy(waitingTime => waitingTime.Name).ToListAsync();
    }

    public async Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var query = db.WaitingTimes.AsNoTracking();
        if (hospital is not null) query = query.Where(waitingTime => waitingTime.Hospital == hospital);
        if (department is not null) query = query.Where(waitingTime => waitingTime.Department == department);
        return await query.OrderBy(waitingTime => waitingTime.Name).ToListAsync();
    }
}
