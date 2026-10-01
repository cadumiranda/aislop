using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain
{
    /// <summary>
    /// Representa uma solicitação de deployment.
    /// </summary>
    public sealed record DeploymentRequest
    {
        /// <summary>Identificador do projeto na Vercel — não o TaskId do AgentTask.</summary>
        public required string ProjectId { get; init; }

        /// <summary>Nome do deployment (aparece na UI da Vercel) — sugerido: derivado do TaskId para rastreabilidade.</summary>
        public required string DeploymentName { get; init; }

        /// <summary>Caminho relativo -> conteúdo do arquivo. Para landing page de Fase 1, tipicamente só "index.html".</summary>
        public required IReadOnlyDictionary<string, string> Files { get; init; }
    }
}
