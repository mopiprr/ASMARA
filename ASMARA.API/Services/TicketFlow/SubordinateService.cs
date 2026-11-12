using ASMARA.API.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Services.TicketFlow;

public static class SubordinateService
{
    /// <summary>
    /// Mengambil ID pegawai yang bersangkutan beserta seluruh ID bawahan langsung maupun berjenjang di bawahnya.
    /// </summary>
    public static async Task<List<Guid>> GetSubordinateAndSelfIdsAsync(
        Guid employeeId,
        IUnitOfWork uow,
        CancellationToken ct = default)
    {
        var employees = await uow.Employees.QueryNoTracking()
            .Where(e => e.TerminatedAt == null)
            .Select(e => new { e.Id, e.ManagerId })
            .ToListAsync(ct);

        var managerLookup = employees
            .Where(e => e.ManagerId.HasValue)
            .GroupBy(e => e.ManagerId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList());

        var result = new HashSet<Guid> { employeeId };
        var queue = new Queue<Guid>();
        queue.Enqueue(employeeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (managerLookup.TryGetValue(current, out var subordinates))
            {
                foreach (var subId in subordinates)
                {
                    if (result.Add(subId))
                    {
                        queue.Enqueue(subId);
                    }
                }
            }
        }

        return result.ToList();
    }
}
