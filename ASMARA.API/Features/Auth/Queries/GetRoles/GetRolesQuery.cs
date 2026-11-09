using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Auth.Queries.GetRoles;

public record GetRolesQuery : IRequest<IReadOnlyList<RoleOptionDto>>;

public class GetRolesQueryHandler(IUnitOfWork uow) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleOptionDto>>
{
    public async Task<IReadOnlyList<RoleOptionDto>> Handle(GetRolesQuery request, CancellationToken ct)
        => await uow.Roles.QueryNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.RoleCode)
            .Select(r => new RoleOptionDto(r.Id, r.RoleCode, r.RoleName, r.Description))
            .ToListAsync(ct);
}