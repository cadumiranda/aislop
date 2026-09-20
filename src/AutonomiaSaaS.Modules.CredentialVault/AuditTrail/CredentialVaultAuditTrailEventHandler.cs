using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace AutonomiaSaaS.Modules.CredentialVault.AuditTrail;

/// <summary>
/// Só rotula o evento para exibição na lista do Admin (coluna "Action") — a gravação em si
/// acontece via IAuditTrailManager.RecordEventAsync, chamado direto de DataProtectionCredentialVault.
/// Padrão confirmado contra a documentação/skill de referência do módulo AuditTrail, não
/// compilado contra o pacote real nesta sessão.
/// </summary>
public sealed class CredentialVaultAuditTrailEventHandler : AuditTrailEventHandlerBase
{
    public override Task CreateAsync(AuditTrailCreateContext context)
    {
        if (context is AuditTrailCreateContext<CredentialStoredAuditEvent> stored)
        {
            stored.AuditTrailEventItem.Action = stored.AuditTrailEventItem.WasRotation ? "Rotacionada" : "Criada";
        }
        else if (context is AuditTrailCreateContext<CredentialRevokedAuditEvent> revoked)
        {
            revoked.AuditTrailEventItem.Action = "Revogada";
        }

        return Task.CompletedTask;
    }
}
