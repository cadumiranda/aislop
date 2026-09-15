using AutonomiaSaaS.Modules.AgentRuntime.Cost;
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

    public async Task<decimal> GetTotalCostForTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var entries = await _store.FindByTaskAsync(taskId, cancellationToken);
        return entries.Sum(e => e.EstimatedCostUsd);
    }

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

    public Task<long> GetAccumulatedTokensForTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
