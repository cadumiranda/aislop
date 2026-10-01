using AutonomiaSaaS.Modules.AcquisitionAgent.Logging;
using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Configuration;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Http;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrchardCore.Modules;
using OrchardCore.Navigation;

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
	    services.AddScoped<IApprovedTaskExecutor, AcquisitionAgentApprovedTaskExecutor>();
        services.AddScoped<INavigationProvider, MainMenu>();

        var vercelOptions = new VercelApiOptions();
        _configuration.GetSection("Vercel").Bind(vercelOptions);
        services.AddSingleton(vercelOptions);

        services.AddTransient<VercelAuthenticationHandler>();

        //if (_hostEnvironment.IsDevelopment())
        //{
        //    services.AddScoped<IDeploymentClient, FakeDeploymentClient>(); // já existe (spec 9.1)
        //}
        //else
        //{
        //    services.AddHttpClient<IDeploymentClient, VercelDeploymentClient>()
        //        .AddHttpMessageHandler<VercelAuthenticationHandler>();
        //}


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

        services.AddScoped<IModularTenantEvents, MyStartupTaskService>();
    }
}