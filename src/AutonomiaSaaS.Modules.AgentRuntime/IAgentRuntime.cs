using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;

namespace AutonomiaSaaS.Modules.AgentRuntime;

/// <summary>
/// O que uma atividade de workflow (ex: PlanLandingPageTask, seção 5 da
/// especificação técnica) precisa fornecer para executar uma chamada de modelo.
/// TaskId e TenantId existem para permitir custo e log associados à tarefa/tenant
/// corretos, sem que este módulo precise conhecer o Content Type AgentTask.
/// </summary>
public sealed record AgentRuntimeRequest(
    string TaskId,
    string TenantId,
    ActivityComplexity Complexity,
    string SystemPrompt,
    string UserMessage,
    int MaxTokens = 2048
);

public sealed record AgentRuntimeResult(
    string Text,
    string ModelUsed,
    int InputTokens,
    int OutputTokens
);

/// <summary>
/// Fachada de alto nível consumida pelas atividades de workflow. Combina
/// IModelRouter (qual modelo usar), IAnthropicClient (como chamar) e
/// ICostLogger (quanto custou), para que quem escreve uma atividade de
/// workflow não precise conhecer nenhum desses três detalhes.
/// </summary>
public interface IAgentRuntime
{
    Task<AgentRuntimeResult> ExecuteAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default);
}
