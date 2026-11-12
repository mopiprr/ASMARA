
using ASMARA.API.Common.Exceptions;
using MediatR;

namespace ASMARA.API.Features.Procurement.Queries.GetPurchaseProof;

public record PurchaseProofFile(
    Stream Content,
    string FileName,
    string ContentType);

public record GetPurchaseProofQuery(Guid TicketId)
    : IRequest<PurchaseProofFile>;

public class GetPurchaseProofQueryHandler
    : IRequestHandler<GetPurchaseProofQuery, PurchaseProofFile>
{
    public Task<PurchaseProofFile> Handle(
        GetPurchaseProofQuery request,
        CancellationToken ct)
    {
        throw new NotFoundException(
            "Purchase proof is not available for this ticket.");
    }
}
