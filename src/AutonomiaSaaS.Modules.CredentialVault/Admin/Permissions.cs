using OrchardCore.Security.Permissions;

namespace AutonomiaSaaS.Modules.CredentialVault.Admin;

public static class CredentialVaultPermissions
{
    public static readonly Permission ManageAgentCredentials = new(
        "ManageAgentCredentials",
        "Gerenciar credenciais de agentes (cofre)");
}

public sealed class Permissions : IPermissionProvider
{
    private static readonly IEnumerable<Permission> AllPermissions = new[]
    {
        CredentialVaultPermissions.ManageAgentCredentials,
    };

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult(AllPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
        new[]
        {
            new PermissionStereotype
            {
                Name = "Administrator",
                Permissions = AllPermissions,
            },
        };
}
