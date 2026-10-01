using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Services;

/// <summary>
/// Implementação em memória / Fake de IDeploymentClient para testes unitários
/// e ambiente de desenvolvimento local sem dependência da API externa da Vercel.
/// </summary>
public sealed class FakeDeploymentClient : IDeploymentClient
{
    public bool ShouldFailStaging { get; set; }
    public bool ShouldFailCheck { get; set; }
    public bool ShouldFailProduction { get; set; }


    public Task<DeploymentResult> DeployToStagingAsync(DeploymentRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFailStaging)
        {
            throw new InvalidOperationException("Falha simulada no deploy de staging.");
        }

        var id = $"staging-{Guid.NewGuid():N}";
        var result = new DeploymentResult() { DeploymentId = id, Url = $"https://staging.example.com/{id}", Status = DeploymentStatus.Healthy, ReadyState = DeploymentReadyState.Ready };
        return Task.FromResult(result);
    }

    public Task<DeploymentHealthResult> CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
    {
        if (ShouldFailCheck)
        {
            var result = new DeploymentHealthResult() { IsHealthy = false, ReadyState = DeploymentReadyState.Error, Url = $"https://staging.example.com/{deploymentId}", TimedOut = false, ErrorDetail = "Falha simulada na verificação de saúde do deployment." };
            return Task.FromResult(result);
        }

        var resultOK = new DeploymentHealthResult() { IsHealthy = true, ReadyState = DeploymentReadyState.Ready, Url = $"https://staging.example.com/{deploymentId}", TimedOut = false, ErrorDetail = null };
        return Task.FromResult(resultOK);
    }

    public Task<DeploymentResult> PromoteToProductionAsync(string projectId, string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFailProduction)
        {
            throw new InvalidOperationException("Falha simulada no deploy de produção.");
        }

        var id = $"prod-{Guid.NewGuid():N}";
        var result = new DeploymentResult() { DeploymentId = id, Url = $"https://app.example.com/{id}", Status = DeploymentStatus.Healthy, ReadyState = DeploymentReadyState.Ready };
        return Task.FromResult(result);
    }

    public Task<DeploymentResult> RollbackToPreviousAsync(string projectId, string previousProductionDeploymentId, CancellationToken cancellationToken = default)
    {
        var result = new DeploymentResult() { DeploymentId = previousProductionDeploymentId, Url = $"https://app.example.com/rollback-{previousProductionDeploymentId}", Status = DeploymentStatus.Healthy, ReadyState = DeploymentReadyState.Ready };
        return Task.FromResult(result);
    }

    Task<DeploymentResult> IDeploymentClient.PromoteToProductionAsync(string projectId, string stagingDeploymentId, CancellationToken cancellationToken)
    {
        return PromoteToProductionAsync(projectId, stagingDeploymentId, cancellationToken);
    }

    Task<DeploymentResult> IDeploymentClient.RollbackToPreviousAsync(string projectId, string previousProductionDeploymentId, CancellationToken cancellationToken)
    {
        return RollbackToPreviousAsync(projectId, previousProductionDeploymentId, cancellationToken);
    }
}
