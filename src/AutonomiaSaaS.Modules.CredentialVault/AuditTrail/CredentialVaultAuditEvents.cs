namespace AutonomiaSaaS.Modules.CredentialVault.AuditTrail;

/// <summary>
/// Item de evento gravado no Audit Trail quando uma credencial é criada ou rotacionada.
/// Nunca inclui o valor — nem cifrado, nem decifrado — só metadados, mesmo princípio de
/// CredentialSummary (ver Abstractions/ICredentialVault.cs).
/// </summary>
public sealed class CredentialStoredAuditEvent
{

    public string AgentName { get; init; }
    public string Key { get; init; }
    public bool WasRotation { get; init; }

    /// <summary>Preenchido pelo CredentialVaultAuditTrailEventHandler, não por quem cria o evento.</summary>
    public string? Action { get; set; }
}

public sealed class CredentialRevokedAuditEvent
{
    public CredentialRevokedAuditEvent()
    {
        
    }

    public string AgentName { get; init; }
    public string Key { get; init; }
    public string? Action { get; set; }
}
