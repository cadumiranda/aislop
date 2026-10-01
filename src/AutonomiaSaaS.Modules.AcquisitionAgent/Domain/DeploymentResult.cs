using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain
{
    /// <summary>
    /// Resultado de uma operação de deploy (staging, produção ou rollback).
    /// DeploymentId é o identificador do provedor externo (Vercel), não o
    /// ContentItemId de uma AgentTask — os dois vivem em espaços diferentes.
    /// </summary>
    public sealed record DeploymentResult
    {
        /// <summary>
        /// Obtém o identificador do deployment no provedor externo (Vercel).
        /// </summary>
        public required string DeploymentId { get; init; }
        /// <summary>
        /// Obtém a URL do deployment no provedor externo (Vercel).
        /// </summary>
        public required string Url { get; init; }
        /// <summary>
        /// Obtém o estado de prontidão do deployment.
        /// </summary>
        public required DeploymentReadyState ReadyState { get; init; }
        /// <summary>
        /// Obtém o status do deployment.
        /// </summary>
        public required DeploymentStatus Status { get; init; }
    }
}
