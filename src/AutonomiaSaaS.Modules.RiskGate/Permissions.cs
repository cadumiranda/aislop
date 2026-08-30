using OrchardCore.Security.Permissions;

namespace AutonomiaSaaS.Modules.RiskGate;

public static class RiskGatePermissions
{
    public static readonly Permission ManageApprovals = new(
        name: "ManageAgentTaskApprovals",
        description: "Aprovar ou rejeitar ações de alto risco propostas por agentes");
}

public sealed class Permissions : IPermissionProvider
{
    private static readonly IReadOnlyList<Permission> AllPermissions = new[]
    {
        RiskGatePermissions.ManageApprovals
    };

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult<IEnumerable<Permission>>(AllPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        yield return new PermissionStereotype
        {
            Name = "Administrator",
            Permissions = AllPermissions
        };
    }
}
