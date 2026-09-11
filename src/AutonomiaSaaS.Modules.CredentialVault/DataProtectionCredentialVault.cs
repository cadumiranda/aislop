using System.Security.Cryptography;
using AutonomiaSaaS.Modules.CredentialVault.Abstractions;
using AutonomiaSaaS.Modules.CredentialVault.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace AutonomiaSaaS.Modules.CredentialVault;

/// <summary>
/// Cofre real, usando Microsoft.AspNetCore.DataProtection — a mesma API que OrchardCore.Email.Smtp
/// já usa internamente para senha de SMTP. Ver README para o porquê de cada decisão.
/// </summary>
public sealed class DataProtectionCredentialVault : ICredentialVault
{
    /// <summary>
    /// Purpose raiz do protetor. Nunca mude esta string depois de credenciais já terem sido
    /// gravadas em produção — mudar a purpose de um IDataProtector invalida tudo que foi cifrado
    /// com a purpose anterior (comportamento padrão e esperado do Data Protection API).
    /// </summary>
    private const string ProtectorPurpose = "AutonomiaSaaS.CredentialVault";

    private readonly IAgentCredentialRecordStore _recordStore;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<DataProtectionCredentialVault> _logger;

    public DataProtectionCredentialVault(
        IAgentCredentialRecordStore recordStore,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DataProtectionCredentialVault> logger)
    {
        _recordStore = recordStore;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public async Task StoreAsync(
        string agentName,
        string key,
        string plaintextValue,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextValue);

        var protector = CreateProtectorFor(agentName);
        var protectedValue = protector.Protect(plaintextValue);

        var existing = await _recordStore.FindAsync(agentName, key, cancellationToken);
        if (existing is not null)
        {
            existing.ProtectedValue = protectedValue;
            existing.Description = description ?? existing.Description;
            existing.RotatedUtc = DateTime.UtcNow;
            await _recordStore.SaveAsync(existing, cancellationToken);
            _logger.LogInformation("Credencial rotacionada: {AgentName}/{Key}.", agentName, key);
            return;
        }

        var record = new AgentCredentialRecord
        {
            AgentName = agentName,
            Key = key,
            ProtectedValue = protectedValue,
            Description = description,
            CreatedUtc = DateTime.UtcNow,
        };
        await _recordStore.SaveAsync(record, cancellationToken);
        _logger.LogInformation("Nova credencial gravada: {AgentName}/{Key}.", agentName, key);
    }

    public async Task<string?> TryGetAsync(string agentName, string key, CancellationToken cancellationToken = default)
    {
        var record = await _recordStore.FindAsync(agentName, key, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var protector = CreateProtectorFor(agentName);
        try
        {
            return protector.Unprotect(record.ProtectedValue);
        }
        catch (CryptographicException ex)
        {
            // Nunca logar record.ProtectedValue nem o valor decifrado aqui — só metadados.
            // Isso acontece se: (a) o AgentName do registro foi alterado sem regravar o valor,
            // (b) as chaves de Data Protection do tenant rotacionaram/foram perdidas, ou
            // (c) o registro pertence a outro tenant (não deveria ser possível via ISession
            // nativamente isolado, mas a falha aqui seria justamente essa proteção funcionando).
            _logger.LogError(
                ex, "Falha ao decifrar credencial {AgentName}/{Key} — tratando como indisponível.",
                agentName, key);
            return null;
        }
    }

    public async Task RevokeAsync(string agentName, string key, CancellationToken cancellationToken = default)
    {
        var record = await _recordStore.FindAsync(agentName, key, cancellationToken);
        if (record is null)
        {
            return; // idempotente
        }
        await _recordStore.DeleteAsync(record, cancellationToken);
        _logger.LogInformation("Credencial revogada: {AgentName}/{Key}.", agentName, key);
    }

    /// <summary>
    /// Um protetor por agente (subPurpose = agentName) — a credencial só é decifrável no
    /// contexto do agente para o qual foi gravada. Ver README, seção "purpose por agente".
    /// </summary>
    private IDataProtector CreateProtectorFor(string agentName)
        => _dataProtectionProvider.CreateProtector(ProtectorPurpose, agentName);
}
