using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.AuditTrail.Services;

namespace AutonomiaSaaS.Modules.CredentialVault.AuditTrail;

/// <summary>
/// Isola OrchardCore.AuditTrail.Services.IAuditTrailManager atrás de uma interface própria —
/// mesma razão de sempre neste projeto (IProcessInvoker no sandbox, IAgentCredentialRecordStore
/// no cofre): não tenho certeza da superfície COMPLETA de IAuditTrailManager além do que o
/// skill de referência do módulo mostrou (só RecordEventAsync foi confirmado). Isolar atrás
/// desta interface fina significa que um fake de teste só precisa implementar um método, não
/// adivinhar o resto de um contrato externo que eu não controlo.
/// </summary>
public interface IAuditTrailRecorder
{
    Task RecordAsync<T>(string name, string category, string correlationId, T eventItem, CancellationToken cancellationToken = default) where T : class, new();
}

/// <summary>
/// Implementação real, contra IAuditTrailManager. Best-effort de propósito: uma falha ao
/// registrar auditoria nunca deveria desfazer ou impedir a operação de negócio que já aconteceu
/// (a credencial já foi gravada/revogada antes desta chamada) — mesmo princípio já aplicado à
/// notificação de expiração de aprovação (RiskGate).
/// </summary>
public sealed class OrchardAuditTrailRecorder : IAuditTrailRecorder
{
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<OrchardAuditTrailRecorder> _logger;

    public OrchardAuditTrailRecorder(
        IAuditTrailManager auditTrailManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<OrchardAuditTrailRecorder> logger)
    {
        _auditTrailManager = auditTrailManager;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task RecordAsync<T>(
        string name, string category, string correlationId, T eventItem, CancellationToken cancellationToken = default) where T : class, new()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            await _auditTrailManager.RecordEventAsync<T>(new OrchardCore.AuditTrail.Services.Models.AuditTrailContext<T>(
                name: name,
                category: category,
                correlationId: correlationId,
                userId: user?.FindFirstValue(ClaimTypes.NameIdentifier),
                userName: user?.Identity?.Name,
                auditTrailEventItem: eventItem));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Falha ao gravar evento de Audit Trail '{EventName}' (correlationId={CorrelationId}).",
                name, correlationId);
        }
    }
}
