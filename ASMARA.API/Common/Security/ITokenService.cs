using ASMARA.API.Entities;

namespace ASMARA.API.Common.Security;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(
        Account account,
        IEnumerable<string> roleCodes);

    string CreateRefreshToken();

    string HashToken(string token);
}
