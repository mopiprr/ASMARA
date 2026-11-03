namespace ASMARA.API.Models.Ticket;

public class ProcurementDetailDto
{
    
}

public record CreateProcurementDetailDto(
    Guid TicketId,
    int ItemCategoryId,
    string ItemName,
    int Quantity,
    decimal Price,
    int? ItemId = null
    );

public record UpdateProcurementDetailDto(
    int ItemCategoryId,
    string ItemName,
    int Quantity,
    decimal Price,
    int? ItemId = null
);

public record ProcurementDetailResponseDto(
    Guid TicketId,
    int? ItemId,
    string? ItemCode,
    int ItemCategoryId,
    string? CategoryName,
    string ItemName,
    int Quantity,
    decimal Price,
    decimal TotalPrice,
    DateTime CreatedAt
);