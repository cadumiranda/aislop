using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace AutonomiaSaaS.Modules.AcquisitionAgent;

/// <summary>
/// Diferente de AdminMenu (módulo RiskGate), este provider responde ao menu
/// "main" — a navegação do site voltado para o usuário final, não o Admin.
/// </summary>
public sealed class MainMenu : INavigationProvider
{
    private readonly IStringLocalizer S;

    public MainMenu(IStringLocalizer<MainMenu> localizer)
    {
        S = localizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, "main", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.CompletedTask;
        }

        builder.Add(S["Gerar landing page"], "1", item => item
            .Action("Index", "LandingPage", new { area = "AutonomiaSaaS.Modules.AcquisitionAgent" })
            .LocalNav());

        return ValueTask.CompletedTask;
    }
}