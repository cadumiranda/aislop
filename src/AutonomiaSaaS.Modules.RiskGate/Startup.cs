using AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using AutonomiaSaaS.Modules.RiskGate.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.BackgroundTasks;

namespace AutonomiaSaaS.Modules.RiskGate;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRiskClassifier, RiskClassifier>();
        services.AddScoped<ITaskRiskGateway, TaskRiskGateway>();

        // TimeProvider.System é o registro padrão do .NET 8 — usar
        // TimeProvider (não DateTimeOffset.UtcNow direto) em TaskRiskGateway
        // é o que permite testar o cálculo de ApprovalTimeout sem depender
        // do relógio real da máquina que roda o teste.
        services.AddSingleton(TimeProvider.System);

        // Peças da UI do Admin (painel de aprovação, seção 8 da
        // especificação técnica): Controller/View são descobertos
        // automaticamente pelo MVC (AddRazorSupportForMvc no .csproj), mas
        // Permissions e AdminMenu precisam ser registrados explicitamente.
        services.AddScoped<IPermissionProvider, Permissions>();
        services.AddScoped<INavigationProvider, AdminMenu>();

        // Trecho a ADICIONAR ao ConfigureServices do Startup.cs REAL de AutonomiaSaaS.Modules.RiskGate —
        // este não é um Startup.cs completo, porque o resto do módulo (ApprovalController, Permissions,
        // AdminMenu, TaskRiskGateway, ActionRiskCatalog, BudgetCapEvaluator) já existe e não foi anexado.
        //


        // Trecho a ADICIONAR ao ConfigureServices do Startup.cs REAL de AutonomiaSaaS.Modules.RiskGate —
        // este não é um Startup.cs completo, porque o resto do módulo (ApprovalController, Permissions,
        // AdminMenu, TaskRiskGateway, ActionRiskCatalog, BudgetCapEvaluator) já existe e não foi anexado.
        //
        // using OrchardCore.BackgroundTasks;
        // using AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;
        // using Microsoft.AspNetCore.Identity;
        // using OrchardCore.Users;

        services.AddScoped<IBackgroundTask, ApprovalExpirationBackgroundTask>();

        // Notificação real via OrchardCore.Notifications, no lugar do stub de log.
        services.AddScoped<IApprovalExpirationRecipientResolver, AdministratorRoleRecipientResolver>();
        services.AddScoped<IApprovalExpirationNotifier, OrchardNotificationApprovalExpirationNotifier>();
    }
}
