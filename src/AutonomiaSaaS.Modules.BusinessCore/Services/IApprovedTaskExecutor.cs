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