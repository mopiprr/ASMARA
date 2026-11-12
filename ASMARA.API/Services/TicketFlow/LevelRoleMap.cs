
using ASMARA.API.Common.Security;

namespace ASMARA.API.Services.TicketFlow;

public static class LevelRoleMap
{
    private static readonly IReadOnlyDictionary<int, string> RoleByLevel =
        new Dictionary<int, string>
        {
            [StepLevels.DivisionManager] = Roles.DivisionManager,
            [StepLevels.Finance] = Roles.Finance,
            [StepLevels.Purchasing] = Roles.Purchasing,
            [StepLevels.Inventory] = Roles.Inventory,
            [StepLevels.ItSupport] = Roles.ItSupport
        };

    public static bool IsRoleLevel(int level) =>
        RoleByLevel.ContainsKey(level);

    public static bool TryGetRole(int level, out string roleCode)
    {
        var found = RoleByLevel.TryGetValue(level, out var role);
        roleCode = role ?? string.Empty;
        return found;
    }

    public static IReadOnlySet<int> LevelsFor(
        IEnumerable<string> roleCodes)
    {
        var roles = roleCodes.ToHashSet();

        return RoleByLevel
            .Where(x => roles.Contains(x.Value))
            .Select(x => x.Key)
            .ToHashSet();
    }
}
