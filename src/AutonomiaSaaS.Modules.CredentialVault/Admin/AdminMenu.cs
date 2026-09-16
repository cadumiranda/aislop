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

    public Task BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        // Padrão comum em módulos Orchard Core para restringir a construção ao menu "admin" —
        // não usei um helper específico de versão porque não pude confirmar sua existência
        // exata na versão real do projeto; esta comparação direta é sempre válida.
        if (!string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        builder
            .Add(S["AutonomiaSaaS"], "10", saas => saas
                .Add(S["Cofre de Credenciais"], "10", credentials => credentials
                    .Action("Index", "AgentCredentialsAdmin", new { area = "AutonomiaSaaS.Modules.CredentialVault" })
                    .Permission(CredentialVaultPermissions.ManageAgentCredentials)
                    .LocalNav()));

        return Task.CompletedTask;
    }
}
