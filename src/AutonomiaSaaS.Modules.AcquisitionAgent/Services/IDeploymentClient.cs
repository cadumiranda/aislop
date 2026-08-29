using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Services;

/// <summary>
/// Abstrai o provedor de deploy (Vercel na especificação técnica, seção 7 do
/// documento de arquitetura). AcquisitionAgentOrchestrator depende só desta
/// interface, nunca do cliente HTTP real — é o que permite testar toda a
/// sequência determinística sem rede.
///
/// Os quatro métodos mapeiam diretamente as atividades da seção 5 da
/// especificação técnica: DeployStagingTask, CheckDeployTask,
/// PromoteToProductionTask e o rollback_action "redeploy_commit_anterior"
/// (seção 5 do documento de arquitetura).
/// </summary>
public interface IDeploymentClient
{
    Task<DeploymentResult> DeployToStagingAsync(string html, CancellationToken cancellationToken = default);

    Task<DeploymentStatus> CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default);

    Task<DeploymentResult> PromoteToProductionAsync(string stagingDeploymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Implementa o rollback_action "redeploy_commit_anterior": promove de
    /// volta o deployment anterior conhecido, usado quando uma promoção para
    /// produção falha depois de aprovada.
    /// </summary>
    Task<DeploymentResult> RollbackToPreviousAsync(string previousDeploymentId, CancellationToken cancellationToken = default);
}
