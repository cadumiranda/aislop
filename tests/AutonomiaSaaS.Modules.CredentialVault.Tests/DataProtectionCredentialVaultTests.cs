using AutonomiaSaaS.Modules.CredentialVault.Storage;

public sealed class FakeAgentCredentialRecordStore : IAgentCredentialRecordStore
{
    private readonly Dictionary<(string AgentName, string Key), AgentCredentialRecord> _records = new();

    public Task<AgentCredentialRecord?> FindAsync(string agentName, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.GetValueOrDefault((agentName, key)));

    public Task<IReadOnlyList<AgentCredentialRecord>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AgentCredentialRecord>>(_records.Values.ToList());

    public Task SaveAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default)
    {
        _records[(record.AgentName, record.Key)] = record;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default)
    {
        _records.Remove((record.AgentName, record.Key));
        return Task.CompletedTask;
    }

    /// <summary>Só para o teste de "cross-agent" — simula um registro cujo AgentName foi alterado sem regravar o valor.</summary>
    public void OverwriteAgentNameDirectly(string oldAgentName, string key, string newAgentName)
    {
        var record = _records[(oldAgentName, key)];
        _records.Remove((oldAgentName, key));
        record.AgentName = newAgentName;
        _records[(newAgentName, key)] = record;
    }
}