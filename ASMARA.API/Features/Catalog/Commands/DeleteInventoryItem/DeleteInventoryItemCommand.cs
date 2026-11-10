using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Commands.DeleteInventoryItem;

public record DeleteInventoryItemCommand(int Id) : IRequest<bool>;

public class DeleteInventoryItemCommandHandler(IUnitOfWork uow)
    : IRequestHandler<DeleteInventoryItemCommand, bool>
{
    public async Task<bool> Handle(DeleteInventoryItemCommand request, CancellationToken ct)
    {
        var item = await uow.ItemInventories.Query()
            .Include(i => i.Assets)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);

        if (item is null)
        {
            throw new NotFoundException($"Inventory item with ID {request.Id} was not found.");
        }

        // Cek apakah ada unit yang sedang dipakai oleh karyawan
        var hasInUseAssets = item.Assets.Any(a =>
            a.Status == (byte)AssetStatusEnum.InUse || a.EmployeeId != null);

        if (hasInUseAssets)
        {
            throw new BusinessRuleException(
                "Cannot delete or deactivate this item because some units are currently assigned to employees or in use.");
        }

        // Soft delete
        item.IsActive = false;
        await uow.SaveChangesAsync(ct);

        return true;
    }
}
