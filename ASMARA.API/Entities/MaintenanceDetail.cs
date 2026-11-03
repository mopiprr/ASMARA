namespace Asmara.API.Entities;

public partial class MaintenanceDetail
{
    public Guid TicketId { get; set; }

    public int AssetId { get; set; }

    public string Description { get; set; } = null!;

    public byte? RepairResult { get; set; }

    public string? HandlerNote { get; set; }

    public string? PartRequirement { get; set; }

    public int? ReplacementAssetId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual Asset? ReplacementAsset { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
}
