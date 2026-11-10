using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Commands.AssignAsset;

public record AssignAssetCommand(int AssetId, AssignAssetRequestDto Request) : IRequest<Unit>;

public class AssignAssetCommandHandler(IUnitOfWork uow) : IRequestHandler<AssignAssetCommand, Unit>
{
    public async Task<Unit> Handle(AssignAssetCommand command, CancellationToken ct)
    {
        var employeeId = command.Request.EmployeeId; // null = Public

        var asset = await uow.Assets.QueryNoTracking()
            .Where(a => a.Id == command.AssetId)
            .Select(a => new { a.EmployeeId, a.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Asset {command.AssetId} was not found.");

        if (employeeId is null)
        {
            if (asset.EmployeeId is null && asset.Status == (byte)AssetStatusEnum.InUse)
                throw new BusinessRuleException("This asset is already public.");
        }
        else
        {
            if (asset.EmployeeId == employeeId)
                throw new BusinessRuleException("This asset is already assigned to the selected employee.");

            var isActiveEmployee = await uow.Employees.QueryNoTracking()
                .AnyAsync(e => e.Id == employeeId
                            && e.EmployeeStatus == (byte)EmployeeStatusEnum.Available
                            && e.Account != null
                            && e.Account.IsActive, ct);

            if (!isActiveEmployee)
                throw new BusinessRuleException("Employee was not found or is not active.");
        }

        if (!await uow.Assets.TryAssignToEmployeeAsync(command.AssetId, employeeId, ct))
            throw new BusinessRuleException("Only Available or In Use assets can be assigned.");

        return Unit.Value;
    }
}