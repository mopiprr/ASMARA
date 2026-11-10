using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Services.Storage;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Procurement.Queries.GetPurchaseProof;

public record PurchaseProofFile(Stream Content, string FileName, string ContentType);

public record GetPurchaseProofQuery(Guid TicketId) : IRequest<PurchaseProofFile>;

public class GetPurchaseProofQueryHandler(IUnitOfWork uow, IFileStorage fileStorage)
    : IRequestHandler<GetPurchaseProofQuery, PurchaseProofFile>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<PurchaseProofFile> Handle(GetPurchaseProofQuery request, CancellationToken ct)
    {
        var proof = await uow.Tickets.QueryNoTracking()
            .Where(t => t.Id == request.TicketId)
            .Select(t => new { t.PurchaseProofFileName, t.PurchaseProofPath })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Ticket {request.TicketId} was not found.");

        if (string.IsNullOrEmpty(proof.PurchaseProofPath) || !fileStorage.Exists(proof.PurchaseProofPath))
            throw new NotFoundException("Purchase proof was not found.");

        var fileName = proof.PurchaseProofFileName ?? Path.GetFileName(proof.PurchaseProofPath);

        if (!ContentTypes.TryGetContentType(fileName, out var contentType))
            contentType = "application/octet-stream";

        return new PurchaseProofFile(fileStorage.OpenRead(proof.PurchaseProofPath), fileName, contentType);
    }
}