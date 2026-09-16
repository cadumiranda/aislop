using AutonomiaSaaS.Modules.CredentialVault.Abstractions;
using AutonomiaSaaS.Modules.CredentialVault.Admin;
using AutonomiaSaaS.Modules.CredentialVault.Storage;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<IIndexProvider, AgentCredentialIndexProvider>();

        services.AddScoped<IAgentCredentialRecordStore, YesSqlAgentCredentialRecordStore>();
        services.AddScoped<ICredentialVault, DataProtectionCredentialVault>();

        // UI de admin (Controller + Views ficam em Admin/, descobertos automaticamente pelo
        // Orchard Core dentro do assembly do módulo — não precisa registrar o Controller em si).
        services.AddScoped<IPermissionProvider, Permissions>();
        services.AddScoped<INavigationProvider, AdminMenu>();
    }
}
