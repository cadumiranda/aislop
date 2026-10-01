using AutonomiaSaaS.Modules.BusinessCore.Parts;

namespace AutonomiaSaaS.Modules.BusinessCore.Services
{
    /// <summary>
    /// Implementação de IApprovedTaskDispatcher que despacha tarefas aprovadas para o executor apropriado com base no AgentName.
    /// </summary>
    public sealed class ApprovedTaskDispatcher : IApprovedTaskDispatcher
    {
        private readonly IReadOnlyDictionary<string, IApprovedTaskExecutor> _executorsByAgentName;

        /// <summary>
        /// Construtor que recebe uma coleção de IApprovedTaskExecutor e os organiza em um dicionário para despacho rápido.
        /// </summary>
        /// <param name="executors"></param>
        public ApprovedTaskDispatcher(IEnumerable<IApprovedTaskExecutor> executors)
        {
            _executorsByAgentName = executors.ToDictionary(e => e.AgentName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Despacha a tarefa aprovada para o executor apropriado com base no AgentName. Lança InvalidOperationException se não houver executor registrado para o AgentName da tarefa.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
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
}
