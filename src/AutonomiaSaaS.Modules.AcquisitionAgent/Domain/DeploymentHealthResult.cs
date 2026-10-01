using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain
{
    /// <summary>
    /// Resultado da verificação de saúde de um deployment.
    /// </summary>
    public sealed record DeploymentHealthResult
    {
        /// <summary>
        /// Obtém um valor que indica se o deployment está saudável.
        /// </summary>
        public required bool IsHealthy { get; init; }
        /// <summary>
        /// Obtém o estado de prontidão do deployment.
        /// </summary>
        public required DeploymentReadyState ReadyState { get; init; }
        /// <summary>
        /// Obtém a URL do deployment.
        /// </summary>
        public string? Url { get; init; }
        /// <summary>
        /// Obtém um valor que indica se o deployment expirou.
        /// </summary>
        public bool TimedOut { get; init; }
        /// <summary>
        /// Obtém detalhes do erro, se houver.
        /// </summary>
        public string? ErrorDetail { get; init; }
    }
}
