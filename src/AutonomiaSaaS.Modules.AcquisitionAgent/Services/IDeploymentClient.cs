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
public interface IDeploymentClient
{
    /// <summary>
    /// Faz o deploy do projeto para staging, retornando imediatamente o ID do deployment.  
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<DeploymentResult> DeployToStagingAsync(DeploymentRequest request, CancellationToken cancellationToken = default);


    /// <summary>Faz polling até READY/ERROR/CANCELED ou timeout — ver README para os valores default.</summary>
    Task<DeploymentHealthResult> CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default);

    /// <summary>Aponta o tráfego de produção do projeto para este deployment. Não refaz o build (comportamento nativo da Vercel).</summary>
    Task<DeploymentResult> PromoteToProductionAsync(string projectId, string stagingDeploymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Implementa o rollback_action "redeploy_commit_anterior": promove de
    /// volta o deployment anterior conhecido, usado quando uma promoção para
    /// produção falha depois de aprovada.
    /// </summary>
    /// <summary>Reverte produção para um deployment anterior específico — quem chama precisa já saber qual era o deployment de produção anterior.</summary>
    Task<DeploymentResult> RollbackToPreviousAsync(string projectId, string previousProductionDeploymentId, CancellationToken cancellationToken = default);
}