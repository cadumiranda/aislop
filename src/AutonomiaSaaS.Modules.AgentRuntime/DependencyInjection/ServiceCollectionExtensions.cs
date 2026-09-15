using AutonomiaSaaS.Modules.AgentRuntime.Cost;
using AutonomiaSaaS.Modules.AgentRuntime.CostLogger;
using AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;
using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;
using AutonomiaSaaS.Modules.AgentRuntime.Pricing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Indexes;

namespace AutonomiaSaaS.Modules.AgentRuntime.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra o módulo AgentRuntime completo: opções, HttpClient nomeado com
    /// headers padrão da Anthropic já aplicados, roteador de modelo, cliente e
    /// fachada de alto nível. Chamado uma vez pelo host (AutonomiaSaaS.Web),
    /// não pelos módulos que consomem IAgentRuntime.
    ///
    /// ICostLogger não é registrado aqui de propósito — a implementação
    /// persistente (associada ao AgentTask real) pertence ao host, que
    /// conhece tanto este módulo quanto o BusinessCore. Quem só quer rodar
    /// isso isolado (testes, execução local) deve registrar InMemoryCostLogger
    /// manualmente antes de chamar este método, ou usar AddAgentRuntimeInMemory
    /// abaixo.
    /// </summary>
    public static IServiceCollection AddAgentRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentRuntimeOptions>(
            configuration.GetSection(AgentRuntimeOptions.SectionName));

        services.AddHttpClient<IAnthropicClient, AnthropicClient>((provider, httpClient) =>
        {
            var options = provider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AgentRuntimeOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new InvalidOperationException(
                    "AgentRuntimeOptions.ApiKey não foi configurada. Em produção ela deve vir " +
                    "do Azure Key Vault via configuração do host, nunca de appsettings.json.");
            }

            httpClient.BaseAddress = new Uri(options.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            httpClient.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", options.ApiVersion);
        });

        services.AddScoped<IIndexProvider, CostLogEntryIndexProvider>();
        services.AddScoped<ICostLogEntryStore, YesSqlCostLogEntryStore>();
        services.AddSingleton<IModelPricingProvider, StaticModelPricingCatalog>();

        services.AddSingleton<IModelRouter, ModelRouter>();
        services.AddScoped<IAgentRuntime, AgentRuntimeService>();

        return services;
    }

    /// <summary>
    /// Atalho para testes/execução local: registra tudo de AddAgentRuntime
    /// mais um ICostLogger em memória, sem depender de nenhuma infraestrutura
    /// externa além da própria API da Anthropic.
    /// </summary>
    public static IServiceCollection AddAgentRuntimeInMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAgentRuntime(configuration);
        services.AddSingleton<ICostLogger, InMemoryCostLogger>();
        return services;
    }

    public static IServiceCollection AddPersistentAgentRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAgentRuntime(configuration);
        services.AddScoped<ICostLogger, PersistentCostLogger>();
        return services;
    }
}
