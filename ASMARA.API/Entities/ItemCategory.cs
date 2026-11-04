namespace ASMARA.API.Entities;

public partial class ItemCategory
{
    public int Id { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public byte TrackingType { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ItemInventory> ItemInventories { get; set; } = new List<ItemInventory>();

    public virtual ICollection<ProcurementDetail> ProcurementDetails { get; set; } = new List<ProcurementDetail>();
}
