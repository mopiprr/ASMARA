namespace ASMARA.API.Entities;

public partial class RefreshToken
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public string TokenHash { get; set; } = null!;

    public bool IsPersistent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}