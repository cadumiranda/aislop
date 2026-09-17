using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;

namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger;

/// <summary>
/// Um registro de custo de uma única chamada ao modelo. TaskId referencia
/// o AgentTask (definido no módulo BusinessCore) que originou a chamada —
/// este módulo não depende do Content Type, só carrega o identificador
/// como string para não criar acoplamento entre AgentRuntime e BusinessCore.
/// </summary>
public sealed record ModelCallCost(
    long TaskId,
    string AgentName,
    string TenantId,
    string Model,
    ActivityComplexity Complexity,
    int InputTokens,
    int OutputTokens,
    DateTimeOffset OccurredAt
);

/// <summary>
/// Registra o custo de cada chamada de modelo. A implementação real (fora
/// desta especificação inicial) deve persistir isso de forma que o teto de
/// custo por tarefa/ciclo (seção 8 da arquitetura) possa ser consultado
/// antes de autorizar a próxima chamada — por isso a interface já expõe
/// GetAccumulatedCostAsync, não só o registro de escrita.
/// </summary>
public interface ICostLogger
{
    Task LogAsync(ModelCallCost cost, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soma de tokens (input + output) já gastos por uma tarefa específica.
    /// Usado por quem chama o AgentRuntime para decidir se ainda há orçamento
    /// antes de disparar mais uma chamada — o teto em si não é aplicado aqui,
    /// é responsabilidade de quem orquestra a atividade de workflow.
    /// </summary>
    Task<long> GetAccumulatedTokensForTaskAsync(long taskId, CancellationToken cancellationToken = default);
}
