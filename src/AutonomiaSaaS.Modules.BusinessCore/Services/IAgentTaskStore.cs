using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;

namespace AutonomiaSaaS.Modules.BusinessCore.Services;

public sealed record CreateAgentTaskRequest(
    string AgentName,
    string Action,
    RiskLevel RiskLevel,
    long EstimatedCostTokens,
    string RollbackAction,
    string PayloadJson = "{}"
);

/// <summary>
/// Ponto único de escrita para AgentTask. Nenhum outro código deve chamar
/// IContentManager.UpdateAsync diretamente sobre um item de AgentTask —
/// é isso que garante que AgentTaskStateMachine.Validate roda sempre antes
/// de qualquer mudança de Status chegar ao banco.
/// </summary>
public interface IAgentTaskStore
{
    /// <summary>
    /// Cria a tarefa em Status = Proposta. Não decide sozinho para qual
    /// próximo estado ela vai — isso é responsabilidade de quem classificou
    /// o risco (módulo RiskGate, ainda não implementado) chamar
    /// TransitionAsync logo em seguida.
    /// </summary>
    Task<long> CreateAsync(CreateAgentTaskRequest request, CancellationToken cancellationToken = default);

    Task<AgentTaskPart?> GetByIdAsync(long taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move a tarefa para um novo status, validando a transição contra
    /// AgentTaskStateMachine antes de persistir. Lança
    /// InvalidStateTransitionException se a transição não for permitida —
    /// deixa o erro subir, não engole silenciosamente.
    /// </summary>
    Task TransitionAsync(long taskId, AgentTaskStatus newStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava o momento em que a aprovação expira. Separado de TransitionAsync
    /// de propósito: ApprovalTimeout é um dado associado ao estado
    /// AguardandoAprovacao, não uma transição de estado em si — misturar os
    /// dois faria AgentTaskStateMachine.Validate rodar para uma operação que
    /// não muda o Status, o que não faz sentido.
    /// Deve ser chamado logo após TransitionAsync(..., AguardandoAprovacao),
    /// nunca antes nem em qualquer outro estado.
    /// </summary>
    Task SetApprovalTimeoutAsync(
        long taskId, DateTimeOffset timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tarefas aguardando aprovação, ordenadas pelo timeout mais próximo de
    /// vencer primeiro — é a consulta que alimenta o painel de aprovação
    /// (seção 8 da especificação técnica).
    /// </summary>
    Task<IReadOnlyList<AgentTaskPart>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tarefas com Status = AguardandoAprovacao cujo ApprovalTimeout já
    /// passou — consumido pelo IBackgroundTask de expiração (seção 5 da
    /// especificação técnica), que em seguida chama TransitionAsync(..., Expirada)
    /// para cada uma.
    /// </summary>
    Task<IReadOnlyList<AgentTaskPart>> GetExpiredApprovalsAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default);
}
