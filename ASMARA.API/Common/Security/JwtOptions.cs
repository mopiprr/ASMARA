namespace ASMARA.API.Common.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 15;

    public int RefreshTokenMinutes { get; set; } = 60;
}