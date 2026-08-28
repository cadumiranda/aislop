using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Indexes;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using OrchardCore.ContentManagement;
using YesSql;

namespace AutonomiaSaaS.Modules.BusinessCore.Services;

public sealed class AgentTaskStore : IAgentTaskStore
{
    private readonly IContentManager _contentManager;
    private readonly ISession _session;

    public AgentTaskStore(IContentManager contentManager, ISession session)
    {
        _contentManager = contentManager;
        _session = session;
    }

    public async Task<string> CreateAsync(
        CreateAgentTaskRequest request, CancellationToken cancellationToken = default)
    {
        var contentItem = await _contentManager.NewAsync("AgentTask");

        contentItem.Alter<AgentTaskPart>(part =>
        {
            part.AgentName = request.AgentName;
            part.Action = request.Action;
            part.RiskLevel = request.RiskLevel;
            part.EstimatedCostTokens = request.EstimatedCostTokens;
            part.RollbackAction = request.RollbackAction;
            part.Status = AgentTaskStatus.Proposta; // toda tarefa nasce em Proposta, sem exceção
        });

        await _contentManager.CreateAsync(contentItem, VersionOptions.Published);

        return contentItem.ContentItemId;
    }

    public async Task<AgentTaskPart?> GetByIdAsync(
        string taskId, CancellationToken cancellationToken = default)
    {
        var contentItem = await _contentManager.GetAsync(taskId, VersionOptions.Latest);
        return contentItem?.As<AgentTaskPart>();
    }

    public async Task TransitionAsync(
        string taskId, AgentTaskStatus newStatus, CancellationToken cancellationToken = default)
    {
        var contentItem = await _contentManager.GetAsync(taskId, VersionOptions.Latest)
            ?? throw new InvalidOperationException($"AgentTask '{taskId}' não encontrada.");

        var part = contentItem.As<AgentTaskPart>()
            ?? throw new InvalidOperationException($"Content item '{taskId}' não tem AgentTaskPart.");

        // A validação é a linha mais importante deste arquivo inteiro: nenhuma
        // escrita de Status acontece sem passar pela máquina de estados.
        AgentTaskStateMachine.Validate(part.Status, newStatus);

        contentItem.Alter<AgentTaskPart>(p =>
        {
            p.Status = newStatus;

            // ApprovalTimeout só faz sentido enquanto a tarefa está aguardando
            // aprovação — limpa ao sair desse estado, para não confundir uma
            // consulta futura de "tarefas vencidas" com uma tarefa já decidida.
            if (newStatus != AgentTaskStatus.AguardandoAprovacao)
            {
                p.ApprovalTimeout = null;
            }
        });

        await _contentManager.UpdateAsync(contentItem);
    }

    public async Task SetApprovalTimeoutAsync(
        string taskId, DateTimeOffset timeout, CancellationToken cancellationToken = default)
    {
        var contentItem = await _contentManager.GetAsync(taskId, VersionOptions.Latest)
            ?? throw new InvalidOperationException($"AgentTask '{taskId}' não encontrada.");

        var part = contentItem.As<AgentTaskPart>()
            ?? throw new InvalidOperationException($"Content item '{taskId}' não tem AgentTaskPart.");

        if (part.Status != AgentTaskStatus.AguardandoAprovacao)
        {
            throw new InvalidOperationException(
                $"Só é permitido definir ApprovalTimeout quando Status = AguardandoAprovacao. " +
                $"Status atual: '{part.Status}'.");
        }

        contentItem.Alter<AgentTaskPart>(p => p.ApprovalTimeout = timeout);
        await _contentManager.UpdateAsync(contentItem);
    }

    public async Task<IReadOnlyList<AgentTaskPart>> GetPendingApprovalAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = await _session
            .Query<ContentItem, AgentTaskPartIndex>()
            .Where(index => index.Status == AgentTaskStatus.AguardandoAprovacao)
            .OrderBy(index => index.ApprovalTimeout)
            .ListAsync();

        return await LoadPartsAsync(indexes);
    }

    public async Task<IReadOnlyList<AgentTaskPart>> GetExpiredApprovalsAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var indexes = await _session
            .Query<ContentItem, AgentTaskPartIndex>()
            .Where(index =>
                index.Status == AgentTaskStatus.AguardandoAprovacao
                && index.ApprovalTimeout != null
                && index.ApprovalTimeout <= now)
            .ListAsync();

        return await LoadPartsAsync(indexes);
    }

    private async Task<IReadOnlyList<AgentTaskPart>> LoadPartsAsync(IEnumerable<ContentItem> contentItems)
    {
        var parts = new List<AgentTaskPart>();
        foreach (var contentItem in contentItems)
        {
            var part = contentItem.As<AgentTaskPart>();
            if (part is not null)
            {
                parts.Add(part);
            }
        }
        return parts;
    }
}
