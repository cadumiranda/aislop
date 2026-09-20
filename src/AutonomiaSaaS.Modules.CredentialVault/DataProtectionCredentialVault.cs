using System.Security.Claims;
using System.Security.Cryptography;
using AutonomiaSaaS.Modules.CredentialVault.Abstractions;
using AutonomiaSaaS.Modules.CredentialVault.AuditTrail;
using AutonomiaSaaS.Modules.CredentialVault.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.AuditTrail.Services;

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
    private const string AuditTrailCategory = "AutonomiaSaaS.CredentialVault";

    private readonly IAgentCredentialRecordStore _recordStore;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly AutonomiaSaaS.Modules.CredentialVault.AuditTrail.IAuditTrailRecorder _auditTrailRecorder;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DataProtectionCredentialVault> _logger;

    public DataProtectionCredentialVault(
        IAgentCredentialRecordStore recordStore,
        IDataProtectionProvider dataProtectionProvider,
        IAuditTrailRecorder auditTrailRecorder,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DataProtectionCredentialVault> logger)
    {
        _recordStore = recordStore;
        _dataProtectionProvider = dataProtectionProvider;
        _auditTrailRecorder = auditTrailRecorder;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task StoreAsync(
        string agentName,
        string key,
        string plaintextValue,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(plaintextValue);

        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextValue);

        var protector = CreateProtectorFor(agentName);
        var protectedValue = protector.Protect(plaintextValue);

        var existing = await _recordStore.FindAsync(agentName, key, cancellationToken);
        var wasRotation = existing is not null;
        if (existing is not null)
        {
            existing.ProtectedValue = protectedValue;
            existing.Description = description ?? existing.Description;
            existing.RotatedUtc = DateTime.UtcNow;
            await _recordStore.SaveAsync(existing, cancellationToken);
            _logger.LogInformation("Credencial rotacionada: {AgentName}/{Key}.", agentName, key);
        }
        else
        {
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

        await RecordAuditEventAsync(
            name: wasRotation ? "CredentialRotated" : "CredentialStored",
            correlationId: BuildCorrelationId(agentName, key),
            auditTrailEventItem: new CredentialStoredAuditEvent
            {
                AgentName = agentName,
                Key = key,
                WasRotation = wasRotation,
            });
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

        // Deliberadamente NÃO auditado: TryGetAsync é chamado por agentes em runtime (ex: o
        // VercelAuthenticationHandler, a cada requisição de deploy). Auditar toda leitura geraria
        // ruído sem valor de compliance real — o que importa registrar é quem alterou uma
        // credencial, não quem a usou pra fazer o trabalho pra qual ela existe.
    }

    public async Task RevokeAsync(string agentName, string key, CancellationToken cancellationToken = default)
    {
        var record = await _recordStore.FindAsync(agentName, key, cancellationToken);
        if (record is null)
        {
            return; // idempotente — e também não audita, já que nada mudou de fato
        }
        await _recordStore.DeleteAsync(record, cancellationToken);
        _logger.LogInformation("Credencial revogada: {AgentName}/{Key}.", agentName, key);

        await RecordAuditEventAsync(
            name: "CredentialRevoked",
            correlationId: BuildCorrelationId(agentName, key),
            auditTrailEventItem: new CredentialRevokedAuditEvent { AgentName = agentName, Key = key });
    }

    public async Task<IReadOnlyList<CredentialSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _recordStore.ListAllAsync(cancellationToken);
        // Mapeamento explícito campo a campo — nunca repassar o objeto inteiro, exatamente para
        // que ProtectedValue não vaze por acidente numa refatoração futura que troque este
        // record por outro com mais campos.
        return records
            .Select(r => new CredentialSummary
            {
                AgentName = r.AgentName,
                Key = r.Key,
                Description = r.Description,
                CreatedUtc = r.CreatedUtc,
                RotatedUtc = r.RotatedUtc,
            })
            .ToList();
    }

    /// <summary>
    /// Um protetor por agente (subPurpose = agentName) — a credencial só é decifrável no
    /// contexto do agente para o qual foi gravada. Ver README, seção "purpose por agente".
    /// </summary>
    private IDataProtector CreateProtectorFor(string agentName)
        => _dataProtectionProvider.CreateProtector(ProtectorPurpose, agentName);

    /// <summary>
    /// Correlação por agente+chave — permite ver, na tela de Audit Trail, o histórico completo
    /// de uma credencial específica (criada, rotacionada N vezes, revogada), não só um evento
    /// isolado sem contexto do que veio antes.
    /// </summary>
    private static string BuildCorrelationId(string agentName, string key) => $"{agentName}:{key}";

    /// <summary>
    /// Uma falha ao gravar o evento de auditoria NUNCA deveria impedir a operação de negócio em
    /// si (a credencial já foi gravada/revogada com sucesso antes desta chamada) — auditoria é
    /// best-effort aqui, mesmo princípio já aplicado à notificação de expiração de aprovação.
    /// </summary>
    private async Task RecordAuditEventAsync<T>(string name, string correlationId, T auditTrailEventItem) where T : class, new()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            // Use the module-local recorder abstraction so tests can inject a fake implementation.
            await _auditTrailRecorder.RecordAsync(
                name,
                AuditTrailCategory,
                correlationId,
                auditTrailEventItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gravar evento de Audit Trail '{EventName}' (correlationId={CorrelationId}).", name, correlationId);
        }
    }
}