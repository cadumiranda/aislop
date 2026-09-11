using AutonomiaSaaS.Modules.BusinessCore.Parts;

namespace AutonomiaSaaS.Modules.BusinessCore.Services;

/// <summary>
/// Implementado por cada módulo de agente (AcquisitionAgent, e futuramente
/// Ads, Operações, etc.) para executar de fato uma AgentTask depois que ela
/// foi aprovada por um humano. Existir aqui em BusinessCore — e não em
/// RiskGate ou na camada de Admin UI — é o que evita uma dependência
/// circular: RiskGate/Admin UI não podem depender de AcquisitionAgent
/// (senão AcquisitionAgent, que já depende de RiskGate, formaria um ciclo),
/// mas todos os módulos de agente já dependem de BusinessCore de qualquer forma.
/// </summary>
public interface IApprovedTaskExecutor
{
    /// <summary>
    /// Nome do agente que esta implementação sabe executar, deve bater
    /// exatamente com AgentTaskPart.AgentName (ex: "acquisition_agent").
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Executa a ação real depois da aprovação. Implementações são
    /// responsáveis por mover a tarefa para Executando/Concluida/Revertida
    /// usando IAgentTaskStore — o dispatcher não faz isso por elas, porque
    /// só o próprio agente sabe interpretar seu PayloadJson e decidir o que
    /// significa sucesso ou falha para aquela ação específica.
    /// </summary>
    Task ExecuteApprovedTaskAsync(AgentTaskPart task, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ponto único que a Admin UI (ou qualquer chamador) usa para, depois de
/// aprovar uma tarefa, disparar a execução real — sem precisar saber qual
/// agente é responsável por cada AgentName. Resolve o executor certo via
/// DI (todos os IApprovedTaskExecutor registrados são injetados aqui).
/// </summary>
public interface IApprovedTaskDispatcher
{
    Task DispatchAsync(AgentTaskPart task, CancellationToken cancellationToken = default);
}

public sealed class ApprovedTaskDispatcher : IApprovedTaskDispatcher
{
    private readonly IReadOnlyDictionary<string, IApprovedTaskExecutor> _executorsByAgentName;

    public ApprovedTaskDispatcher(IEnumerable<IApprovedTaskExecutor> executors)
    {
        _executorsByAgentName = executors.ToDictionary(e => e.AgentName, StringComparer.OrdinalIgnoreCase);
    }

    public Task DispatchAsync(AgentTaskPart task, CancellationToken cancellationToken = default)
    {
        if (!_executorsByAgentName.TryGetValue(task.AgentName, out var executor))
        {
            // Nenhum executor registrado para este AgentName — normalmente
            // significa um agente novo cujo módulo ainda não implementou
            // IApprovedTaskExecutor. Lançar aqui é intencional: aprovar uma
            // tarefa e ela simplesmente não fazer nada, silenciosamente,
            // seria pior do que falhar de forma visível no painel.
            throw new InvalidOperationException(
                $"Nenhum IApprovedTaskExecutor registrado para o agente '{task.AgentName}'. " +
                "O módulo desse agente precisa implementar e registrar essa interface.");
        }

        return executor.ExecuteApprovedTaskAsync(task, cancellationToken);
    }
}
