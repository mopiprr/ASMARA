namespace ASMARA.API.Models.Ticket;

public class ProcurementDetailDto
{
    
}

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