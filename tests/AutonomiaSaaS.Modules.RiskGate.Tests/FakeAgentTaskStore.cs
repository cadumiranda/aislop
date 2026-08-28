using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

/// <summary>
/// Fake em memória de IAgentTaskStore, que REPRODUZ a validação da máquina
/// de estados real (AgentTaskStateMachine.Validate) — importante que o fake
/// não seja "permissivo demais", senão um teste de TaskRiskGateway poderia
/// passar mesmo se o gateway tentasse uma transição inválida.
/// </summary>
internal sealed class FakeAgentTaskStore : IAgentTaskStore
{
    private sealed class StoredTask
    {
        public required string Id { get; init; }
        public required AgentTaskPart Part { get; init; }
    }

    private readonly Dictionary<string, StoredTask> _tasks = new();
    private int _nextId = 1;

    public IReadOnlyDictionary<string, AgentTaskPart> AllTasks
        => _tasks.ToDictionary(kv => kv.Key, kv => kv.Value.Part);

    public Task<string> CreateAsync(CreateAgentTaskRequest request, CancellationToken cancellationToken = default)
    {
        var id = $"task_{_nextId++}";
        var part = new AgentTaskPart
        {
            AgentName = request.AgentName,
            Action = request.Action,
            RiskLevel = request.RiskLevel,
            EstimatedCostTokens = request.EstimatedCostTokens,
            RollbackAction = request.RollbackAction,
            Status = AgentTaskStatus.Proposta
        };
        _tasks[id] = new StoredTask { Id = id, Part = part };
        return Task.FromResult(id);
    }

    public Task<AgentTaskPart?> GetByIdAsync(string taskId, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.TryGetValue(taskId, out var t) ? t.Part : null);

    public Task TransitionAsync(string taskId, AgentTaskStatus newStatus, CancellationToken cancellationToken = default)
    {
        var task = _tasks[taskId];
        AgentTaskStateMachine.Validate(task.Part.Status, newStatus); // mesma regra do store real
        task.Part.Status = newStatus;
        if (newStatus != AgentTaskStatus.AguardandoAprovacao)
        {
            task.Part.ApprovalTimeout = null;
        }
        return Task.CompletedTask;
    }

    public Task SetApprovalTimeoutAsync(string taskId, DateTimeOffset timeout, CancellationToken cancellationToken = default)
    {
        var task = _tasks[taskId];
        if (task.Part.Status != AgentTaskStatus.AguardandoAprovacao)
        {
            throw new InvalidOperationException("Só é permitido definir ApprovalTimeout em AguardandoAprovacao.");
        }
        task.Part.ApprovalTimeout = timeout;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentTaskPart>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AgentTaskPart>>(
            _tasks.Values.Select(t => t.Part)
                .Where(p => p.Status == AgentTaskStatus.AguardandoAprovacao)
                .OrderBy(p => p.ApprovalTimeout)
                .ToList());

    public Task<IReadOnlyList<AgentTaskPart>> GetExpiredApprovalsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AgentTaskPart>>(
            _tasks.Values.Select(t => t.Part)
                .Where(p => p.Status == AgentTaskStatus.AguardandoAprovacao
                            && p.ApprovalTimeout is not null
                            && p.ApprovalTimeout <= now)
                .ToList());
}
