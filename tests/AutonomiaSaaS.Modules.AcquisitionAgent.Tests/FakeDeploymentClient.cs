using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;
using AutonomiaSaaS.Modules.AcquisitionAgent.Services;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Tests;

internal sealed class FakeDeploymentClient : IDeploymentClient
{
    public DeploymentStatus StagingHealthAfterDeploy { get; set; } = DeploymentStatus.Healthy;
    public DeploymentStatus ProductionHealthAfterPromote { get; set; } = DeploymentStatus.Healthy;
    public bool ThrowOnPromote { get; set; } = false;
    public bool RollbackCalled { get; private set; }
    public string? LastRollbackDeploymentId { get; private set; }

    public Task<DeploymentResult> DeployToStagingAsync(DeploymentRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new DeploymentResult
        {
            DeploymentId = "staging_dep_1",
            Url = "https://staging.example.com",
            ReadyState = DeploymentReadyState.Ready,
            Status = DeploymentStatus.Healthy,
        });

    public Task<DeploymentHealthResult> CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(new DeploymentHealthResult
        {
            IsHealthy = StagingHealthAfterDeploy == DeploymentStatus.Healthy,
            ReadyState = DeploymentReadyState.Ready,
            Url = "https://staging.example.com",
            TimedOut = false,
            ErrorDetail = null,
        });

    public Task<DeploymentResult> PromoteToProductionAsync(string projectId, string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPromote)
        {
            throw new InvalidOperationException("Falha simulada na promoção para produção.");
        }

        return Task.FromResult(new DeploymentResult
        {
            DeploymentId = "prod_dep_1",
            Url = "https://producao.example.com",
            ReadyState = DeploymentReadyState.Ready,
            Status = ProductionHealthAfterPromote,
        });
    }

    public Task<DeploymentResult> RollbackToPreviousAsync(string projectId, string previousDeploymentId, CancellationToken cancellationToken = default)
    {
        RollbackCalled = true;
        LastRollbackDeploymentId = previousDeploymentId;
        return Task.FromResult(new DeploymentResult
        {
            DeploymentId = previousDeploymentId,
            Url = "https://staging.example.com",
            ReadyState = DeploymentReadyState.Ready,
            Status = DeploymentStatus.Healthy,
        });
    }
}
