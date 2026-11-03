namespace Asmara.API.Entities;

public partial class ItemInventory
{
    public int Id { get; set; }

    public int ItemCategoryId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? Qty { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual ItemCategory ItemCategory { get; set; } = null!;

    public virtual ICollection<ProcurementDetail> ProcurementDetails { get; set; } = new List<ProcurementDetail>();
}
