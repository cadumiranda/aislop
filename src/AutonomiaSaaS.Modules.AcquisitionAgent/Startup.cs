using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.AcquisitionAgent;

public sealed class Startup : StartupBase
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAcquisitionAgentOrchestrator, AcquisitionAgentOrchestrator>();

        var vercelToken = _configuration["Vercel:ApiToken"];
        var useFake = _configuration.GetValue<bool>("Vercel:UseFake", defaultValue: true);

        if (!useFake && !string.IsNullOrEmpty(vercelToken))
        {
            services.AddHttpClient<IDeploymentClient, VercelDeploymentClient>(httpClient =>
            {
                httpClient.BaseAddress = new Uri("https://api.vercel.com");
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", vercelToken);
            });
        }
        else
        {
            services.AddSingleton<IDeploymentClient, FakeDeploymentClient>();
        }
    }
}
