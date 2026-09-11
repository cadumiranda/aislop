using AutonomiaSaaS.Modules.CredentialVault.Abstractions;
using AutonomiaSaaS.Modules.CredentialVault.Storage;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Modules;
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
    }
}
