namespace AutonomiaSaaS.Modules.CredentialVault.Abstractions;

/// <summary>
/// Metadados de uma credencial, SEM o valor cifrado nem decifrado — usado para telas de
/// listagem. O tipo não carrega o ciphertext de propósito: mesmo um bug que serializasse este
/// objeto inteiro pra tela não vazaria nada além de metadados.
/// </summary>
public sealed record CredentialSummary
{
    public required string AgentName { get; init; }
    public required string Key { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public DateTime? RotatedUtc { get; init; }
}

/// <summary>
/// Cofre de credenciais por agente. Nunca expõe o valor em texto puro em log — só o chamador,
/// que pediu explicitamente o valor decifrado, deve manuseá-lo (e não deveria logá-lo também).
/// </summary>
public interface ICredentialVault
{
    /// <summary>
    /// Grava ou substitui (rotaciona) uma credencial. Chamar de novo com o mesmo
    /// agentName/key sobrescreve o valor anterior — não acumula histórico de credenciais velhas.
    /// </summary>
    Task StoreAsync(
        string agentName,
        string key,
        string plaintextValue,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devolve o valor em texto puro, ou null se a credencial não existir OU se não puder ser
    /// decifrada (ex: AgentName do registro foi alterado sem regravar o valor). As duas situações
    /// são indistinguíveis de propósito para quem chama — nenhuma delas deveria ser tratada
    /// diferente de "credencial indisponível".
    /// </summary>
    Task<string?> TryGetAsync(string agentName, string key, CancellationToken cancellationToken = default);

    /// <summary>Remove a credencial. Idempotente — chamar para uma chave inexistente não é erro.</summary>
    Task RevokeAsync(string agentName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista metadados de todas as credenciais do tenant atual — nunca o valor. Usado pela tela
    /// de admin; nunca deveria ser usado por um agente em runtime (agentes chamam TryGetAsync
    /// sabendo exatamente qual credencial querem, não navegam a lista).
    /// </summary>
    Task<IReadOnlyList<CredentialSummary>> ListAsync(CancellationToken cancellationToken = default);
}
