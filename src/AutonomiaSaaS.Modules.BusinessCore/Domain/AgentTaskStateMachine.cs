namespace AutonomiaSaaS.Modules.BusinessCore.Domain;

/// <summary>
/// Implementa exatamente o diagrama de estados da seção 6 do documento de
/// arquitetura. Deliberadamente uma classe estática e pura — sem I/O, sem
/// dependência de Orchard Core, sem async — porque essa é a peça mais barata
/// de testar exaustivamente e a mais cara de deixar errada (uma transição
/// inválida permitida por engano pode fazer uma tarefa de alto risco
/// executar sem aprovação).
///
/// Quem persiste o novo estado (AgentTaskStore, camada acima) é responsável
/// por chamar Validate antes de gravar, nunca gravar um novo Status sem
/// passar por aqui.
/// </summary>
public static class AgentTaskStateMachine
{
    private static readonly IReadOnlyDictionary<AgentTaskStatus, AgentTaskStatus[]> AllowedTransitions =
        new Dictionary<AgentTaskStatus, AgentTaskStatus[]>
        {
            [AgentTaskStatus.Proposta] = new[]
            {
                AgentTaskStatus.AguardandoAprovacao,   // ação de alto risco
                AgentTaskStatus.AutoExecutando          // ação de baixo risco
            },
            [AgentTaskStatus.AguardandoAprovacao] = new[]
            {
                AgentTaskStatus.Aprovada,
                AgentTaskStatus.Rejeitada,
                AgentTaskStatus.Expirada               // timeout, seção 4 da arquitetura
            },
            [AgentTaskStatus.Aprovada] = new[]
            {
                AgentTaskStatus.Executando
            },
            [AgentTaskStatus.AutoExecutando] = new[]
            {
                AgentTaskStatus.Concluida,
                AgentTaskStatus.Revertida               // rollback_action disparado, seção 5
            },
            [AgentTaskStatus.Executando] = new[]
            {
                AgentTaskStatus.Concluida,
                AgentTaskStatus.Revertida
            },

            // Estados terminais: nenhuma transição de saída.
            [AgentTaskStatus.Concluida] = Array.Empty<AgentTaskStatus>(),
            [AgentTaskStatus.Rejeitada] = Array.Empty<AgentTaskStatus>(),
            [AgentTaskStatus.Expirada] = Array.Empty<AgentTaskStatus>(),
            [AgentTaskStatus.Revertida] = Array.Empty<AgentTaskStatus>(),
        };

    private static readonly HashSet<AgentTaskStatus> TerminalStatuses = new(
        AllowedTransitions.Where(kv => kv.Value.Length == 0).Select(kv => kv.Key));

    public static bool IsTerminal(AgentTaskStatus status) => TerminalStatuses.Contains(status);

    public static bool CanTransition(AgentTaskStatus from, AgentTaskStatus to)
        => AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>
    /// Lança InvalidStateTransitionException se a transição não for permitida.
    /// Usar sempre antes de persistir um novo Status.
    /// </summary>
    public static void Validate(AgentTaskStatus from, AgentTaskStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidStateTransitionException(from, to);
        }
    }

    public static IReadOnlyCollection<AgentTaskStatus> GetAllowedNextStates(AgentTaskStatus from)
        => AllowedTransitions.TryGetValue(from, out var allowed)
            ? allowed
            : Array.Empty<AgentTaskStatus>();
}
