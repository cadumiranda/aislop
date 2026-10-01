using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain
{
    /// <summary>
    /// Representa o estado de prontidão de um deployment.
    /// </summary>
    public enum DeploymentReadyState
    {
        /// <summary>
        /// O deployment está na fila para ser processado.
        /// </summary>
        Queued,
        /// <summary>
        /// O deployment está em processo de inicialização.
        /// </summary>
        Initializing,
        /// <summary>
        /// O deployment está em processo de construção.
        /// </summary>
        Building,
        /// <summary>
        /// O deployment está em processo de implantação.
        /// </summary>
        Ready,
        /// <summary>
        /// O deployment encontrou um erro durante o processo.
        /// </summary>
        Error,
        /// <summary>
        /// O deployment foi cancelado antes de ser concluído.
        /// </summary>
        Canceled,
    }
}
