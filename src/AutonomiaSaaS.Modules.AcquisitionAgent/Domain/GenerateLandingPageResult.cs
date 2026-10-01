using AutonomiaSaaS.Modules.BusinessCore.Domain;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain;

/// <summary>
/// Resultado de GenerateLandingPageAsync. Contém dois TaskIds distintos e de
/// propósito: LandingPageTaskId é a tarefa de baixo risco (geração + deploy
/// em staging, já concluída ou revertida quando este record é retornado);
/// ProductionApprovalTaskId é a tarefa de alto risco que fica aguardando
/// decisão humana antes de promover para produção (seção 4 e 5 da arquitetura).
/// </summary>
public sealed record GenerateLandingPageResult(
    long LandingPageTaskId,
    AgentTaskStatus LandingPageTaskStatus,
    string? StagingUrl,
    string? StagingDeploymentId,
    long? ProductionApprovalTaskId
);