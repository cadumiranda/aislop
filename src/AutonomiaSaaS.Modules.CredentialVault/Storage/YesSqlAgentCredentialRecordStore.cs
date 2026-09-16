using YesSql;

namespace AutonomiaSaaS.Modules.CredentialVault.Storage;

/// <summary>
/// Implementação real, contra YesSql — não coberta por teste unitário nesta entrega (ver README).
/// `ISession` já é resolvido por tenant pelo host Orchard Core, então o isolamento entre tenants
/// aqui é automático — não precisa (e não deveria) filtrar por tenant manualmente neste código.
/// </summary>
public sealed class YesSqlAgentCredentialRecordStore : IAgentCredentialRecordStore
{
    private readonly ISession _session;

    public YesSqlAgentCredentialRecordStore(ISession session)
    {
        _session = session;
    }

    public Task<AgentCredentialRecord?> FindAsync(string agentName, string key, CancellationToken cancellationToken = default)
        => _session.Query<AgentCredentialRecord, AgentCredentialIndex>(
                index => index.AgentName == agentName && index.Key == key)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<AgentCredentialRecord>> ListAllAsync(CancellationToken cancellationToken = default)
        => (await _session.Query<AgentCredentialRecord>().ListAsync()).ToList();

    public Task SaveAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default)
    {
        // NOTA: algumas versões de YesSql expõem SaveAsync(object, CancellationToken) — se o
        // pacote usado no BusinessCore já tiver essa sobrecarga, prefira-a. Save() síncrono é
        // suportado por qualquer versão e só enfileira a gravação (efetivada no commit da sessão,
        // gerenciado pelo host Orchard Core ao final do escopo/request).
        _session.Save(record);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default)
    {
        _session.Delete(record);
        return Task.CompletedTask;
    }
}