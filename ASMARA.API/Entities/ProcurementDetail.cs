using System;
using System.Collections.Generic;

namespace Asmara.API.Entities;

public partial class ProcurementDetail
{
    public int Id { get; set; }

    public Guid TicketId { get; set; }

    public int? ItemId { get; set; }

    public int ItemCategoryId { get; set; }

    public string ItemName { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ItemInventory? Item { get; set; }

    public virtual ItemCategory ItemCategory { get; set; } = null!;

    public virtual Ticket Ticket { get; set; } = null!;
}
