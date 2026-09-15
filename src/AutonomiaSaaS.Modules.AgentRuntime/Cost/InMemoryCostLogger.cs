using System.Collections.Concurrent;

namespace AutonomiaSaaS.Modules.AgentRuntime.Cost;

/// <summary>
/// Implementação em memória, pensada para testes unitários e para rodar a
/// Fase 1 localmente sem depender de banco configurado. A persistência real
/// (associada ao AgentTask do módulo BusinessCore, sobrevivendo a reinício
/// do processo) é responsabilidade de uma implementação registrada por cima
/// desta, no módulo de composição do host — não pertence a AgentRuntime,
/// que não conhece o Content Type AgentTask.
/// </summary>
public sealed class InMemoryCostLogger : ICostLogger
{
    private readonly ConcurrentDictionary<long, long> _tokensByTask = new();
    private readonly ConcurrentBag<ModelCallCost> _history = new();

    public Task LogAsync(ModelCallCost cost, CancellationToken cancellationToken = default)
    {
        _history.Add(cost);
        _tokensByTask.AddOrUpdate(
            cost.TaskId,
            addValue: cost.InputTokens + cost.OutputTokens,
            updateValueFactory: (_, existing) => existing + cost.InputTokens + cost.OutputTokens);

        return Task.CompletedTask;
    }

    public Task<long> GetAccumulatedTokensForTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        _tokensByTask.TryGetValue(taskId, out var total);
        return Task.FromResult(total);
    }

    /// <summary>
    /// Exposto só para inspeção em testes — não faz parte de ICostLogger
    /// de propósito, para não vazar detalhe de implementação para quem
    /// consome a interface em produção.
    /// </summary>
    internal IReadOnlyCollection<ModelCallCost> History => _history;
}
