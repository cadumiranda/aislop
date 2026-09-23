using AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using AutonomiaSaaS.Modules.RiskGate.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.BackgroundTasks;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using AutonomiaSaaS.Modules.RiskGate.Logging;

namespace AutonomiaSaaS.Modules.RiskGate;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAgentTaskStore, AgentTaskStore>();

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
        // using OrchardCore.BackgroundTasks;
        // using AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;
        // using Microsoft.AspNetCore.Identity;
        // using OrchardCore.Users;
        services.AddScoped<ITaskRiskGateway, TaskRiskGateway>();
        services.AddScoped<IBackgroundTask, ApprovalExpirationBackgroundTask>();

        // Notificação real via OrchardCore.Notifications, no lugar do stub de log.
        services.AddScoped<IApprovalExpirationRecipientResolver, AdministratorRoleRecipientResolver>();
        services.AddScoped<IApprovalExpirationNotifier, OrchardNotificationApprovalExpirationNotifier>();

        // Mantenha o registro abaixo comentado como alternativa apenas para ambiente local sem o
        // módulo OrchardCore.Notifications habilitado:
        services.AddSingleton<IApprovalExpirationNotifier, LoggingApprovalExpirationNotifier>();

        // ============================================================================
        // PRÉ-REQUISITOS DE MÓDULO/FEATURE (fora do código, no Manifest de dependências ou no recipe
        // de setup do tenant):
        //   - Habilitar a feature "OrchardCore.Notifications" (traz INotificationService;
        //     UserManager<IUser> já vem de "OrchardCore.Users", que presumo já habilitado).
        //   - Habilitar "OrchardCore.Notifications.Email" (ou o provider de canal escolhido) para que
        //     a notificação realmente chegue a alguém — sem isso, SendAsync só grava na central de
        //     notificações do admin, sem alcançar o usuário fora do app.
        //
        // PRÉ-REQUISITO DE INFRA (se for escalar para múltiplas instâncias):
        //   - O LockTimeout/LockExpiration do [BackgroundTask] só previne execução duplicada entre
        //     instâncias com um provedor de lock distribuído configurado (ex: OrchardCore.Redis).
        //     Com uma única instância, isso já não importa.
        // ============================================================================
        services.AddScoped<IModularTenantEvents, MyStartupTaskService>();
    }
}
