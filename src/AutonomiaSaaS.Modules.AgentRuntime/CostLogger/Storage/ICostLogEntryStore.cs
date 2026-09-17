namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;

/// <summary>Mesma razão de sempre: isolar YesSql atrás de uma interface pra testar a lógica de custo sem banco real.</summary>
public interface ICostLogEntryStore
{
    Task AddAsync(CostLogEntryRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CostLogEntryRecord>> FindByTaskAsync(long taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CostLogEntryRecord>> FindByAgentSinceAsync(string agentName, DateTimeOffset since, CancellationToken cancellationToken = default);
}
