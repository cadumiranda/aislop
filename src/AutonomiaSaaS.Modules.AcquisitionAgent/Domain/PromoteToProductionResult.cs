using AutonomiaSaaS.Modules.BusinessCore.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain
{
    /// <summary>
    /// Resultado de PromoteToProductionAsync, chamado depois que um humano
    /// aprova a AgentTask de deploy em produção (seção 8 da especificação
    /// técnica: botão "Aprovar" no painel de aprovação).
    /// </summary>
    public sealed record PromoteToProductionResult(
        AgentTaskStatus FinalStatus,
        string? ProductionUrl
    );

}
