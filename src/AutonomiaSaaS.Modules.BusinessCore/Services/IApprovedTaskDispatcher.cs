using AutonomiaSaaS.Modules.BusinessCore.Parts;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.BusinessCore.Services
{
    /// <summary>
    /// Ponto único que a Admin UI (ou qualquer chamador) usa para, depois de
    /// aprovar uma tarefa, disparar a execução real — sem precisar saber qual
    /// agente é responsável por cada AgentName. Resolve o executor certo via
    /// DI (todos os IApprovedTaskExecutor registrados são injetados aqui).
    /// </summary>
    public interface IApprovedTaskDispatcher
    {
        /// <summary>
        /// Dispara a execução de uma AgentTask depois que ela foi aprovada por um humano.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DispatchAsync(AgentTaskPart task, CancellationToken cancellationToken = default);
    }
}
