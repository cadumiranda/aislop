using AutonomiaSaaS.Modules.BusinessCore.Indexes;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace AutonomiaSaaS.Modules.BusinessCore;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<BusinessContextPart>();
        services.AddContentPart<AgentTaskPart>();

        services.AddSingleton<IIndexProvider, AgentTaskPartIndexProvider>();
        services.AddDataMigration<Migrations>();

        services.AddScoped<IAgentTaskStore, AgentTaskStore>();
        services.AddScoped<IBusinessContextStore, BusinessContextStore>();
    }
}
