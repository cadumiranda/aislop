namespace AutonomiaSaaS.Modules.CredentialVault.Storage;

/// <summary>
/// Isola o acesso a ISession/YesSql atrás de uma interface — mesma razão de sempre (IProcessInvoker
/// no módulo de sandbox, IAgentTaskStore no de expiração): a lógica de criptografia do vault fica
/// testável sem precisar de um YesSql real, e a parte que realmente depende de YesSql fica pequena
/// e isolada o bastante pra revisar com atenção redobrada.
/// </summary>
public interface IAgentCredentialRecordStore
{
    Task<AgentCredentialRecord?> FindAsync(string agentName, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentCredentialRecord>> ListAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default);
    Task DeleteAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default);
}