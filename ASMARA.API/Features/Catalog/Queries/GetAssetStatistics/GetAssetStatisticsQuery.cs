using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetAssetStatistics;

public record GetAssetStatisticsQuery : IRequest<AssetStatisticsDto>;

public class GetAssetStatisticsQueryHandler(IUnitOfWork uow) : IRequestHandler<GetAssetStatisticsQuery, AssetStatisticsDto>
{
    public async Task<AssetStatisticsDto> Handle(GetAssetStatisticsQuery request, CancellationToken ct)
    {
        var counts = await uow.Assets.QueryNoTracking()
            .Where(a => a.Item.IsActive &&
                        a.Status != (byte)AssetStatusEnum.Disposed &&
                        a.Status != (byte)AssetStatusEnum.Archived)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(AssetStatusEnum status)
            => counts.Where(c => c.Status == (byte)status).Sum(c => c.Count);

        return new AssetStatisticsDto(
            counts.Sum(c => c.Count),
            CountOf(AssetStatusEnum.Available),
            CountOf(AssetStatusEnum.InUse),
            CountOf(AssetStatusEnum.UnderMaintenance));
    }
}