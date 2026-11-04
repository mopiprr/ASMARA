using MediatR;

namespace ASMARA.API.Features.Dashboard.Queries.GetDashboardSummary;

public record GetDashboardSummaryQuery(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    string Email,
    string[] Roles
) : IRequest<DashboardSummaryResponse>;