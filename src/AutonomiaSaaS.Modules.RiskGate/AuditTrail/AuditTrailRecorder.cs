using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.AuditTrail.Services;

namespace AutonomiaSaaS.Modules.RiskGate.AuditTrail;

/// <summary>
/// Mesma interface e mesmo raciocínio de AutonomiaSaaS.Modules.CredentialVault.AuditTrail —
/// duplicada aqui de propósito, não compartilhada, porque RiskGate e CredentialVault não têm
/// (e não deveriam ganhar só por causa disto) uma dependência um do outro. Se um dia isso
/// aparecer pela terceira vez em outro módulo, aí sim vale extrair para um pacote comum.
/// </summary>
public interface IAuditTrailRecorder
{
    Task RecordAsync<T>(string name, string category, string correlationId, T eventItem, CancellationToken cancellationToken = default) where T : class, new();
}

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