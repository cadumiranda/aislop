using YesSql;

namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;

/// <summary>Não coberto por teste unitário nesta entrega — ver README do módulo do cofre para a mesma justificativa.</summary>
public sealed class YesSqlCostLogEntryStore : ICostLogEntryStore
{
    private readonly ISession _session;

    public YesSqlCostLogEntryStore(ISession session)
    {
        _session = session;
    }

    public Task AddAsync(CostLogEntryRecord record, CancellationToken cancellationToken = default)
    {
        _session.Save(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CostLogEntryRecord>> FindByTaskAsync(long taskId, CancellationToken cancellationToken = default)
        => QueryAsIReadOnlyListAsync(_session.Query<CostLogEntryRecord, CostLogEntryIndex>(i => i.TaskId == taskId));

    public Task<IReadOnlyList<CostLogEntryRecord>> FindByAgentSinceAsync(
        string agentName, DateTimeOffset since, CancellationToken cancellationToken = default)
        => QueryAsIReadOnlyListAsync(_session.Query<CostLogEntryRecord, CostLogEntryIndex>(
            i => i.AgentName == agentName && i.CreatedUtc >= since.UtcDateTime));

    private static async Task<IReadOnlyList<CostLogEntryRecord>> QueryAsIReadOnlyListAsync(IQuery<CostLogEntryRecord> query)
        => (await query.ListAsync()).ToList();
}
