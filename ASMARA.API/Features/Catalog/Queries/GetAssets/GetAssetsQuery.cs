using ASMARA.API.Common.Paging;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;

namespace ASMARA.API.Features.Catalog.Queries.GetAssets;

public record GetAssetsQuery(
    AssetStatusEnum? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool Desc = false,
    int Page = 1,
    int PageSize = 10)
    : IRequest<PagedResult<AssetListItemDto>>;

public class GetAssetsQueryHandler(IUnitOfWork uow) : IRequestHandler<GetAssetsQuery, PagedResult<AssetListItemDto>>
{
    private const byte InUse = (byte)AssetStatusEnum.InUse;
    private const byte UnderMaintenance = (byte)AssetStatusEnum.UnderMaintenance;

    public async Task<PagedResult<AssetListItemDto>> Handle(GetAssetsQuery request, CancellationToken ct)
    {
        var query = uow.Assets.QueryNoTracking()
            .Where(a => a.Item.IsActive &&
                        a.Status != (byte)AssetStatusEnum.Disposed &&
                        a.Status != (byte)AssetStatusEnum.Archived);

        if (request.Status.HasValue)
        {
            var status = (byte)request.Status.Value;
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(a =>
                a.AssetTag.Contains(search) ||
                a.Item.ItemName.Contains(search) ||
                (a.Employee != null && a.Employee.FullName.Contains(search)));
        }

        return await ApplySorting(query, request)
            .ThenBy(a => a.Id)   // supaya urutan paging stabil
            .Select(a => new AssetListItemDto(
                a.Id,
                a.AssetTag,
                a.Item.ItemName,
                a.Item.ItemCategory.CategoryName,
                a.Status,
                a.EmployeeId,
                a.Employee != null ? a.Employee.FullName : null,
                a.Employee != null ? a.Employee.Department.DepartmentName : null,
                a.EmployeeId == null && (a.Status == InUse || a.Status == UnderMaintenance)))
            .ToPagedResultAsync(request.Page, request.PageSize, ct: ct);
    }

    private static IOrderedQueryable<Asset> ApplySorting(IQueryable<Asset> query, GetAssetsQuery request)
        => request.SortBy?.ToLowerInvariant() switch
        {
            "itemname" => request.Desc
                ? query.OrderByDescending(a => a.Item.ItemName)
                : query.OrderBy(a => a.Item.ItemName),

            "category" => request.Desc
                ? query.OrderByDescending(a => a.Item.ItemCategory.CategoryName)
                : query.OrderBy(a => a.Item.ItemCategory.CategoryName),

            "holder" => request.Desc
                ? query.OrderByDescending(a => a.Employee!.FullName)
                : query.OrderBy(a => a.Employee!.FullName),

            "status" => request.Desc
                ? query.OrderByDescending(a => a.Status)
                : query.OrderBy(a => a.Status),

            _ => request.Desc
                ? query.OrderByDescending(a => a.AssetTag)
                : query.OrderBy(a => a.AssetTag)
        };
}