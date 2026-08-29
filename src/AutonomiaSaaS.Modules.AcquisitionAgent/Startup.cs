using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.AcquisitionAgent;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAcquisitionAgentOrchestrator, AcquisitionAgentOrchestrator>();

        services.AddHttpClient<IDeploymentClient, VercelDeploymentClient>(httpClient =>
        {
            // Base URL e token de autenticação da Vercel devem vir de
            // configuração/Key Vault (mesmo princípio do módulo AgentRuntime),
            // nunca hardcoded. Deixado como placeholder — ver aviso em
            // VercelDeploymentClient sobre a API não ter sido validada.
            httpClient.BaseAddress = new Uri("https://api.vercel.com");
        });
    }
}
