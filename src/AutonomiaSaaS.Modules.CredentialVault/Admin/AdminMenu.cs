using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace AutonomiaSaaS.Modules.CredentialVault.Admin;

public sealed class AdminMenu : INavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        builder
            .Add(S["AutonomiaSaaS"], "10", saas => saas
                .Add(S["Cofre de Credenciais"], "10", credentials => credentials
                    .Action("Index", "AgentCredentialsAdmin", new { area = "AutonomiaSaaS.Modules.CredentialVault" })
                    .Permission(CredentialVaultPermissions.ManageAgentCredentials)
                    .LocalNav()));

        return default;
    }
}