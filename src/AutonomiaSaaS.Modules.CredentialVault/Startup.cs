using AutonomiaSaaS.Modules.CredentialVault.Abstractions;
using AutonomiaSaaS.Modules.CredentialVault.Admin;
using AutonomiaSaaS.Modules.CredentialVault.AuditTrail;
using AutonomiaSaaS.Modules.CredentialVault.Storage;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.AuditTrail.Services;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using YesSql.Indexes;

namespace AutonomiaSaaS.Modules.CredentialVault;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // IDataProtectionProvider já é registrado pelo host ASP.NET Core / Orchard Core —
        // não precisamos (e não deveríamos) registrar isso aqui, só consumir via DI.

        // CORRIGIDO: IIndexProvider precisa ser Singleton, nunca Scoped. O AddDataAccess() do
        // Orchard Core resolve IEnumerable<IIndexProvider> a partir do container RAIZ, na
        // criação do shell do tenant — não de um escopo de request. Registrar como Scoped
        // quebra com "Cannot resolve scoped service 'IEnumerable<IIndexProvider>' from root
        // provider" (mesmo erro documentado na issue #7847 do próprio OrchardCMS/OrchardCore).
        // A suposição anterior de que existiria um `services.AddIndexProvider<T>()` pronto
        // estava sinalizada como não confirmada — use este registro explícito no lugar dela.
        services.AddSingleton<IIndexProvider, AgentCredentialIndexProvider>();

        services.AddScoped<IAgentCredentialRecordStore, YesSqlAgentCredentialRecordStore>();
        services.AddScoped<ICredentialVault, DataProtectionCredentialVault>();

        // TryAddSingleton por baixo dos panos — seguro chamar mesmo que o host já tenha
        // registrado isso (bem provável, já que é comum em qualquer app ASP.NET Core).
        services.AddHttpContextAccessor();

        // [NOVO] Audit Trail: registra criação/rotação/revogação de credencial como evento
        // auditável. Requer a feature OrchardCore.AuditTrail habilitada — ver Manifest.cs,
        // que agora declara essa dependência explicitamente (módulo não liga sem ela).
        services.AddScoped<IAuditTrailEventHandler, CredentialVaultAuditTrailEventHandler>();
        services.AddScoped<IAuditTrailRecorder, OrchardAuditTrailRecorder>();
        // Register OrchardCore's IAuditTrailManager for the real recorder implementation
        services.AddScoped<IAuditTrailManager>(sp => sp.GetRequiredService<OrchardCore.AuditTrail.Services.IAuditTrailManager>());

        // UI de admin (Controller + Views ficam em Admin/, descobertos automaticamente pelo
        // Orchard Core dentro do assembly do módulo — não precisa registrar o Controller em si).
        services.AddScoped<IPermissionProvider, Permissions>();
        services.AddScoped<INavigationProvider, AdminMenu>();
    }
}