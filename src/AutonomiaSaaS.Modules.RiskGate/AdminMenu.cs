using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace AutonomiaSaaS.Modules.RiskGate;

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
            return ValueTask.CompletedTask;
        }

        builder.Add(S["Autonomia SaaS"], autonomiaSaas => autonomiaSaas
            .Add(S["Aprovações"], "1", approvals => approvals
                .Action("Index", "Approval", new { area = "AutonomiaSaaS.Modules.RiskGate" })
                .Permission(RiskGatePermissions.ManageApprovals)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}