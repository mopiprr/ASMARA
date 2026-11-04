using MediatR;

namespace ASMARA.API.Features.Profile.Queries.GetMyProfile;

public record GetMyProfileQuery(Guid EmployeeId)
    : IRequest<MyProfileResponse?>;