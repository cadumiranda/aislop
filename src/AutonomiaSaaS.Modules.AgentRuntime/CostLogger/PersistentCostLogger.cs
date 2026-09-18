using AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;
using AutonomiaSaaS.Modules.AgentRuntime.Pricing;

namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger;

/// <summary>
/// Substitui InMemoryCostLogger. O custo é calculado e gravado no momento do LogAsync (não
/// recalculado na leitura) — ver README, "por que gravar custo no momento do log".
/// </summary>
public sealed class PersistentCostLogger : ICostLogger
{
    private readonly ICostLogEntryStore _store;
    private readonly IModelPricingProvider _pricingProvider;

    public PersistentCostLogger(ICostLogEntryStore store, IModelPricingProvider pricingProvider)
    {
        _store = store;
        _pricingProvider = pricingProvider;
    }

    public async Task LogAsync(ModelCallCost entry, CancellationToken cancellationToken = default)
    {
        var rate = _pricingProvider.GetRate(entry.Model);
        var cost = ComputeCost(entry.InputTokens, entry.OutputTokens, rate);

        var record = new CostLogEntryRecord
        {
            TaskId = entry.TaskId,
            AgentName = entry.AgentName,
            Model = entry.Model,
            InputTokens = entry.InputTokens,
            OutputTokens = entry.OutputTokens,
            EstimatedCostUsd = cost,
            CreatedUtc = DateTime.UtcNow,
        };

        await _store.AddAsync(record, cancellationToken);
    }

    /// <summary>
    /// Soma de custo (USD) de todas as chamadas de modelo registradas para uma tarefa específica.
    /// </summary>
    /// <param name="taskId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<decimal> GetTotalCostForTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var entries = await _store.FindByTaskAsync(taskId, cancellationToken);
        return entries.Sum(e => e.EstimatedCostUsd);
    }

    /// <summary>
    /// Soma de custo (USD) de um agente desde uma data — para o teto "por ciclo do orquestrador" (arquitetura, seção 8).
    /// </summary>
    /// <param name="agentName"></param>
    /// <param name="since"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<decimal> GetTotalCostSinceAsync(string agentName, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var entries = await _store.FindByAgentSinceAsync(agentName, since, cancellationToken);
        return entries.Sum(e => e.EstimatedCostUsd);
    }

    private static decimal ComputeCost(long inputTokens, long outputTokens, ModelRate rate)
    {
        const decimal tokensPerMillion = 1_000_000m;
        var inputCost = inputTokens / tokensPerMillion * rate.InputPerMillionTokens;
        var outputCost = outputTokens / tokensPerMillion * rate.OutputPerMillionTokens;
        return inputCost + outputCost;
    }

    /// <summary>
    /// [NOVO] Soma bruta de tokens (input + output) das chamadas registradas para uma tarefa —
    /// sem passar por IModelPricingProvider. Reaproveita o mesmo FindByTaskAsync que
    /// GetTotalCostForTaskAsync usa; a diferença é só o que se soma a partir do mesmo conjunto
    /// de registros, não uma consulta nova.
    /// </summary>
    public async Task<long> GetAccumulatedTokensForTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var entries = await _store.FindByTaskAsync(taskId, cancellationToken);
        return entries.Sum(e => e.InputTokens + e.OutputTokens);
    }
}
