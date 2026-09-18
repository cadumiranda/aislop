using AutonomiaSaaS.Modules.BusinessCore.Domain;

namespace AutonomiaSaaS.Modules.RiskGate.ViewModels;

/// <summary>
/// Nunca expor AgentTaskPart diretamente na View — o ViewModel existe para
/// controlar exatamente o que aparece no painel e evitar acoplar a Razor
/// View à forma interna do Content Part (que pode mudar por outros motivos,
/// como adicionar campos que não fazem sentido mostrar aqui).
/// </summary>
public sealed class PendingApprovalItemViewModel
{
    public required long TaskId { get; init; }
    public required string AgentName { get; init; }
    public required string Action { get; init; }
    public required long EstimatedCostTokens { get; init; }
    public required string RollbackAction { get; init; }
    public required DateTimeOffset? ApprovalTimeout { get; init; }
}

public sealed class ApprovalIndexViewModel
{
    public required IReadOnlyList<PendingApprovalItemViewModel> PendingApprovals { get; init; }
}
