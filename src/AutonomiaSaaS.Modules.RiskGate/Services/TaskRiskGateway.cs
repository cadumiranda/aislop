using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using AutonomiaSaaS.Modules.RiskGate.Domain;

namespace AutonomiaSaaS.Modules.RiskGate.Services;

public sealed record ProposeActionRequest(
    string AgentName,
    string ActionKey,
    string ActionDescription,
    long EstimatedCostTokens,
    string RollbackAction,
    decimal? SpendAmount = null,
    string? SpendCapKey = null,
    TimeSpan? ApprovalTimeout = null
);

public sealed record ProposeActionResult(
    string TaskId,
    RiskLevel RiskLevel,
    AgentTaskStatus Status,
    string ClassificationReason
);

/// <summary>
/// Ponto de entrada único para um agente propor uma ação. Implementa o fluxo
/// completo da seção 4 do documento de arquitetura: cria a tarefa em Proposta,
/// classifica o risco, e move para AguardandoAprovacao (alto risco) ou
/// AutoExecutando (baixo risco) — nunca deixa uma tarefa parada em Proposta.
///
/// Este é o serviço que um agente futuro (AcquisitionAgent, por exemplo)
/// deve chamar em vez de usar IAgentTaskStore diretamente — assim a
/// classificação de risco nunca é pulada por engano.
/// </summary>
public interface ITaskRiskGateway
{
    Task<ProposeActionResult> ProposeActionAsync(
        ProposeActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class TaskRiskGateway : ITaskRiskGateway
{
    private static readonly TimeSpan DefaultApprovalTimeout = TimeSpan.FromHours(48);

    private readonly IAgentTaskStore _agentTaskStore;
    private readonly IRiskClassifier _riskClassifier;
    private readonly TimeProvider _timeProvider;

    public TaskRiskGateway(
        IAgentTaskStore agentTaskStore,
        IRiskClassifier riskClassifier,
        TimeProvider timeProvider)
    {
        _agentTaskStore = agentTaskStore;
        _riskClassifier = riskClassifier;
        _timeProvider = timeProvider;
    }

    public async Task<ProposeActionResult> ProposeActionAsync(
        ProposeActionRequest request, CancellationToken cancellationToken = default)
    {
        var classification = await _riskClassifier.ClassifyAsync(
            new RiskClassificationRequest(request.ActionKey, request.SpendAmount, request.SpendCapKey),
            cancellationToken).ConfigureAwait(false);

        var taskId = await _agentTaskStore.CreateAsync(
            new CreateAgentTaskRequest(
                AgentName: request.AgentName,
                Action: request.ActionDescription,
                RiskLevel: classification.RiskLevel,
                EstimatedCostTokens: request.EstimatedCostTokens,
                RollbackAction: request.RollbackAction),
            cancellationToken).ConfigureAwait(false);

        AgentTaskStatus finalStatus;

        if (classification.RiskLevel == RiskLevel.Alto)
        {
            finalStatus = AgentTaskStatus.AguardandoAprovacao;
            await _agentTaskStore.TransitionAsync(taskId, finalStatus, cancellationToken)
                .ConfigureAwait(false);

            // ApprovalTimeout precisa ser gravado separadamente porque
            // TransitionAsync (BusinessCore) só conhece Status, não os demais
            // campos de AgentTaskPart. Isso é aceitável aqui porque a
            // gravação do timeout não é uma transição de estado — é um dado
            // associado ao estado AguardandoAprovacao, não o próprio estado.
            var timeout = _timeProvider.GetUtcNow() + (request.ApprovalTimeout ?? DefaultApprovalTimeout);
            await _agentTaskStore.SetApprovalTimeoutAsync(taskId, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            finalStatus = AgentTaskStatus.AutoExecutando;
            await _agentTaskStore.TransitionAsync(taskId, finalStatus, cancellationToken)
                .ConfigureAwait(false);
        }

        return new ProposeActionResult(taskId, classification.RiskLevel, finalStatus, classification.Reason);
    }
}
