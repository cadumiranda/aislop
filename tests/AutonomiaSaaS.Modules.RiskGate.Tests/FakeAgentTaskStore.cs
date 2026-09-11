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

    public List<AgentTaskPart> ExpiredTasksToReturn { get; set; } = new();
    public List<long> TransitionedTaskIds { get; } = new();
    public HashSet<long> TaskIdsThatThrowOnTransition { get; } = new();
    public bool ThrowOnGetExpired { get; set; }

    private sealed class StoredTask
    {
        public required long Id { get; init; }
        public required AgentTaskPart Part { get; init; }
    }

    private readonly Dictionary<long, StoredTask> _tasks = new();
    private int _nextId = 1;

    public IReadOnlyDictionary<long, AgentTaskPart> AllTasks
        => _tasks.ToDictionary(kv => kv.Key, kv => kv.Value.Part);

    public Task<long> CreateAsync(CreateAgentTaskRequest request, CancellationToken cancellationToken = default)
    {
        var id = (long)_nextId++;
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

    public Task<AgentTaskPart?> GetByIdAsync(long taskId, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.TryGetValue(taskId, out var t) ? t.Part : null);

    public Task TransitionAsync(long taskId, AgentTaskStatus newStatus, CancellationToken cancellationToken = default)
    {
        if (TaskIdsThatThrowOnTransition.Contains(taskId))
        {
            throw new InvalidOperationException($"falha simulada ao transicionar {taskId}");
        }

        if (_tasks.TryGetValue(taskId, out var stored))
        {
            AgentTaskStateMachine.Validate(stored.Part.Status, newStatus); // mesma regra do store real
            stored.Part.Status = newStatus;
            if (newStatus != AgentTaskStatus.AguardandoAprovacao)
            {
                stored.Part.ApprovalTimeout = null;
            }
        }
        else
        {
            // Support tests that populate ExpiredTasksToReturn without registering them in _tasks.
            var part = ExpiredTasksToReturn.FirstOrDefault(p => p.TaskId.HasValue && p.TaskId.Value == taskId);
            if (part is not null)
            {
                AgentTaskStateMachine.Validate(part.Status, newStatus);
                part.Status = newStatus;
                if (newStatus != AgentTaskStatus.AguardandoAprovacao)
                {
                    part.ApprovalTimeout = null;
                }
            }
            else
            {
                // If unknown taskId, mimic real store behaviour by throwing
                throw new KeyNotFoundException($"Task {taskId} not found");
            }
        }

        TransitionedTaskIds.Add(taskId);
        return Task.CompletedTask;
    }

    public Task SetApprovalTimeoutAsync(long taskId, DateTimeOffset timeout, CancellationToken cancellationToken = default)
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
    {
        if (ThrowOnGetExpired)
        {
            throw new InvalidOperationException("falha simulada ao obter aprovações expiradas");
        }

        // If tests provided an explicit list to return, prefer it.
        if (ExpiredTasksToReturn is not null && ExpiredTasksToReturn.Count > 0)
        {
            return Task.FromResult<IReadOnlyList<AgentTaskPart>>(ExpiredTasksToReturn);
        }

        var list = _tasks.Values.Select(t => t.Part)
            .Where(p => p.Status == AgentTaskStatus.AguardandoAprovacao
                        && p.ApprovalTimeout is not null
                        && p.ApprovalTimeout <= now)
            .ToList();
        return Task.FromResult<IReadOnlyList<AgentTaskPart>>(list);
    }
}
