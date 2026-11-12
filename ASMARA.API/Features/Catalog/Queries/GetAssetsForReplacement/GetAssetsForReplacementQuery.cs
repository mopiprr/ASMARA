using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetAssetsForReplacement;

public record GetAssetsForReplacementQuery(Guid TicketId) : IRequest<IReadOnlyCollection<ReplacementAssetOptionDto>>;

public class GetAssetsForReplacementQueryHandler(IUnitOfWork uow) : IRequestHandler<GetAssetsForReplacementQuery, IReadOnlyCollection<ReplacementAssetOptionDto>>
{
    public async Task<IReadOnlyCollection<ReplacementAssetOptionDto>> Handle(GetAssetsForReplacementQuery command, CancellationToken ct)
    {
        var brokenCategoryId = await uow.MaintenanceDetails.QueryNoTracking()
            .Where(d => d.TicketId == command.TicketId)
            .Select(d => (int?)d.Asset.Item.ItemCategoryId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

        return await uow.Assets.QueryNoTracking()
            .Where(a => a.Status == (byte)AssetStatusEnum.Available
                     && a.Item.ItemCategoryId == brokenCategoryId)
            .OrderBy(a => a.AssetTag)
            .Select(a => new ReplacementAssetOptionDto(
                a.Id,
                a.AssetTag,
                a.Item.ItemName,
                a.Item.ItemCategory.CategoryName))
            .ToListAsync(ct);
    }
}