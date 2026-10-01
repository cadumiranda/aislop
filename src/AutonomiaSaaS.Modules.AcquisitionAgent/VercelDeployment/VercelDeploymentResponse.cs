using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment
{
    /// <summary>
    /// Representa a resposta da API do Vercel após a criação de um deployment.
    /// </summary>
    public sealed class VercelDeploymentResponse
    {
        /// <summary>
        /// Obtém ou define o ID do deployment.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Obtém ou define a URL do deployment.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// Obtém ou define o estado de prontidão do deployment.
        /// </summary>
        public string? ReadyState { get; set; }
    }
}
