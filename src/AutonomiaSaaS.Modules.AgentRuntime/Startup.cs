using AutonomiaSaaS.Modules.AgentRuntime.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.AgentRuntime;

/// <summary>
/// Sem este arquivo, IAgentRuntime nunca era registrado no container de DI
/// do Orchard Core — AcquisitionAgent.Startup só registra
/// IAcquisitionAgentOrchestrator e IDeploymentClient, assumindo que
/// IAgentRuntime já existiria. Esse era um bug real, encontrado só ao montar
/// o host de verdade (AutonomiaSaaS.Web) e perceber que nada chamava
/// AddAgentRuntime em lugar nenhum.
///
/// Usa AddAgentRuntimeInMemory (não AddAgentRuntime) por enquanto: o
/// ICostLogger persistente de verdade (associado ao AgentTask real do
/// BusinessCore) ainda não foi implementado — ver aviso no README do módulo
/// AgentRuntime. Trocar isso é o próximo passo pendente de maior impacto.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPersistentAgentRuntime(_configuration);
    }
}