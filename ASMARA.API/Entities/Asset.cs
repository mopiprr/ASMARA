using System;
using System.Collections.Generic;

namespace Asmara.API.Entities;

public partial class Asset
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string AssetTag { get; set; } = null!;

    public byte Status { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual ItemInventory Item { get; set; } = null!;

    public virtual ICollection<MaintenanceDetail> MaintenanceDetailAssets { get; set; } = new List<MaintenanceDetail>();

    public virtual ICollection<MaintenanceDetail> MaintenanceDetailReplacementAssets { get; set; } = new List<MaintenanceDetail>();
}
