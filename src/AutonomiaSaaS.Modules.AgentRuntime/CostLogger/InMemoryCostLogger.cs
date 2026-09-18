using AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;
using AutonomiaSaaS.Modules.AgentRuntime.Pricing;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using YesSql;

namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger;

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

    private readonly IModelPricingProvider _pricingProvider;

    public InMemoryCostLogger(IModelPricingProvider pricingProvider)
    {
        _pricingProvider = pricingProvider;
    }

    /// <summary>
    /// Registra o custo de uma chamada de modelo, somando os tokens (input + output) para a tarefa correspondente.
    /// </summary>
    /// <param name="cost">cost</param>
    /// <param name="cancellationToken">cancellation Token</param>
    /// <returns></returns>
    public Task LogAsync(ModelCallCost cost, CancellationToken cancellationToken = default)
    {
        _history.Add(cost);
        _tokensByTask.AddOrUpdate(
            cost.TaskId,
            addValue: cost.InputTokens + cost.OutputTokens,
            updateValueFactory: (_, existing) => existing + cost.InputTokens + cost.OutputTokens);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Soma de tokens (input + output) já gastos por uma tarefa específica.
    /// </summary>
    /// <param name="taskId">task Id</param>
    /// <param name="cancellationToken">cancellation Token</param>
    /// <returns></returns>
    public Task<long> GetAccumulatedTokensForTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        _tokensByTask.TryGetValue(taskId, out var total);
        return Task.FromResult(total);
    }

    /// <summary>
    /// Soma de custo (USD) de todas as chamadas de modelo registradas para uma tarefa específica.
    /// </summary>
    /// <param name="taskId">task Id</param>
    /// <param name="cancellationToken">cancellation Token</param>
    /// <returns></returns>
    public Task<decimal> GetTotalCostForTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var entry = _history.FirstOrDefault(i => i.TaskId == taskId);

        var rate = _pricingProvider.GetRate(entry.Model);
        var cost = ComputeCost(entry.InputTokens, entry.OutputTokens, rate);

        return Task.FromResult(cost);
    }

    /// <summary>
    /// Calcula o custo total de chamadas de modelo para um agente específico a partir de uma data.
    /// </summary>
    /// <param name="agentName">agent Name</param>
    /// <param name="since">since</param>
    /// <param name="cancellationToken">cancellation Token</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task<decimal> GetTotalCostSinceAsync(string agentName, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var entries = _history.Where(i => i.AgentName == agentName).Select(entry => new CostLogEntryRecord
        {
            TaskId = entry.TaskId,
            AgentName = entry.AgentName,
            Model = entry.Model,
            InputTokens = entry.InputTokens,
            OutputTokens = entry.OutputTokens,
            EstimatedCostUsd = ComputeCost(entry.InputTokens, entry.OutputTokens, _pricingProvider.GetRate(entry.Model)),
            CreatedUtc = DateTime.UtcNow,
        });

        return Task.FromResult(entries.Sum(e => e.EstimatedCostUsd));
    }


    /// <summary>
    /// Exposto só para inspeção em testes — não faz parte de ICostLogger
    /// de propósito, para não vazar detalhe de implementação para quem
    /// consome a interface em produção.
    /// </summary>
    internal IReadOnlyCollection<ModelCallCost> History => _history;

    private static decimal ComputeCost(long inputTokens, long outputTokens, ModelRate rate)
    {
        const decimal tokensPerMillion = 1_000_000m;
        var inputCost = inputTokens / tokensPerMillion * rate.InputPerMillionTokens;
        var outputCost = outputTokens / tokensPerMillion * rate.OutputPerMillionTokens;
        return inputCost + outputCost;
    }
}
