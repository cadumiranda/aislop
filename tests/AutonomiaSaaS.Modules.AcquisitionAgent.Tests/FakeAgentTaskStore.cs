using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Tests;

internal sealed class FakeAgentTaskStore : IAgentTaskStore
{
    private readonly Dictionary<string, AgentTaskPart> _tasks = new();

    /// <summary>
    /// Permite ao teste registrar uma tarefa pré-existente com um Status
    /// específico (ex: Aprovada), simulando que ela já passou pelo painel
    /// de aprovação antes de PromoteToProductionAsync ser chamado.
    /// </summary>
    public void Seed(string taskId, AgentTaskStatus status)
        => _tasks[taskId] = new AgentTaskPart { Status = status };

    public IReadOnlyDictionary<string, AgentTaskPart> AllTasks => _tasks;

    public Task<string> CreateAsync(CreateAgentTaskRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "Este fake não é usado via CreateAsync — o orquestrador cria tarefas através de " +
            "ITaskRiskGateway (ver FakeTaskRiskGateway), não diretamente via IAgentTaskStore.");

    public Task<AgentTaskPart?> GetByIdAsync(string taskId, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.TryGetValue(taskId, out var part) ? part : null);

    public Task TransitionAsync(string taskId, AgentTaskStatus newStatus, CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var part))
        {
            // Tarefa criada via FakeTaskRiskGateway, que não compartilha
            // estado com este fake — não temos como saber o Status real que
            // ela tinha antes desta chamada, então a primeira transição
            // observada aqui é aceita sem validação (ela já teria sido
            // validada de verdade dentro do TaskRiskGateway real). Só a
            // partir da segunda transição sobre a mesma tarefa é que este
            // fake passa a aplicar AgentTaskStateMachine.Validate.
            _tasks[taskId] = new AgentTaskPart { Status = newStatus };
            return Task.CompletedTask;
        }

        AgentTaskStateMachine.Validate(part.Status, newStatus);
        part.Status = newStatus;
        return Task.CompletedTask;
    }

    public Task SetApprovalTimeoutAsync(string taskId, DateTimeOffset timeout, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<AgentTaskPart>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AgentTaskPart>>(Array.Empty<AgentTaskPart>());

    public Task<IReadOnlyList<AgentTaskPart>> GetExpiredApprovalsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AgentTaskPart>>(Array.Empty<AgentTaskPart>());
}
