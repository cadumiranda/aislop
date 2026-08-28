using AutonomiaSaaS.Modules.AgentRuntime.Models;

namespace AutonomiaSaaS.Modules.AgentRuntime;

/// <summary>
/// Cliente fino sobre o endpoint /v1/messages. Não conhece conceito de tarefa,
/// tenant ou custo acumulado — isso é responsabilidade de camadas acima
/// (AgentRuntimeService, seção 6 da especificação técnica). Mantém esta
/// interface pequena de propósito para ser fácil de trocar de implementação
/// (ex: um SDK oficial, se um dia existir para .NET) sem afetar o resto do sistema.
/// </summary>
public interface IAnthropicClient
{
    Task<AnthropicMessagesResponse> SendAsync(
        AnthropicMessagesRequest request,
        CancellationToken cancellationToken = default);
}
