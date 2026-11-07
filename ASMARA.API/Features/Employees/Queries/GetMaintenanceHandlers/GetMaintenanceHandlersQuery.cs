using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Employees.Queries.GetMaintenanceHandlers;

public record GetMaintenanceHandlersQuery : IRequest<IReadOnlyList<HandlerOptionDto>>;

public class GetMaintenanceHandlersQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetMaintenanceHandlersQuery, IReadOnlyList<HandlerOptionDto>>
{
    public async Task<IReadOnlyList<HandlerOptionDto>> Handle(GetMaintenanceHandlersQuery request, CancellationToken ct)
        => await uow.Employees.GetActiveHandlers()
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .Select(e => new HandlerOptionDto(e.Id, e.EmployeeNumber, e.FullName))
            .ToListAsync(ct);
}