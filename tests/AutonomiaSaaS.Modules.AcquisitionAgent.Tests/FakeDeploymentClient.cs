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

    public Task<DeploymentResult> DeployToStagingAsync(string html, CancellationToken cancellationToken = default)
        => Task.FromResult(new DeploymentResult("staging_dep_1", "https://staging.example.com", DeploymentStatus.Healthy));

    public Task<DeploymentStatus> CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(StagingHealthAfterDeploy);

    public Task<DeploymentResult> PromoteToProductionAsync(string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPromote)
        {
            throw new InvalidOperationException("Falha simulada na promoção para produção.");
        }

        return Task.FromResult(new DeploymentResult(
            "prod_dep_1", "https://producao.example.com", ProductionHealthAfterPromote));
    }

    public Task<DeploymentResult> RollbackToPreviousAsync(string previousDeploymentId, CancellationToken cancellationToken = default)
    {
        RollbackCalled = true;
        LastRollbackDeploymentId = previousDeploymentId;
        return Task.FromResult(new DeploymentResult(previousDeploymentId, "https://staging.example.com", DeploymentStatus.Healthy));
    }
}
