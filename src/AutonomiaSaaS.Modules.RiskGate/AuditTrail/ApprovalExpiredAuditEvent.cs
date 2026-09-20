namespace AutonomiaSaaS.Modules.RiskGate.AuditTrail;

public sealed class ApprovalExpiredAuditEvent
{
    public ApprovalExpiredAuditEvent()
    {
            
    }

    public long TaskId { get; init; }
    public string AgentName { get; init; }
}
