using System;
using System.Threading;
using System.Threading.Tasks;
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

    public Task<DeploymentResult> DeployToStagingAsync(string html, CancellationToken cancellationToken = default)
    {
        if (ShouldFailStaging)
        {
            throw new InvalidOperationException("Falha simulada no deploy de staging.");
        }

        var id = $"staging-{Guid.NewGuid():N}";
        var result = new DeploymentResult(id, $"https://staging.example.com/{id}", DeploymentStatus.Healthy);
        return Task.FromResult(result);
    }

    public Task<DeploymentStatus> CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFailCheck)
        {
            return Task.FromResult(DeploymentStatus.Unhealthy);
        }

        return Task.FromResult(DeploymentStatus.Healthy);
    }

    public Task<DeploymentResult> PromoteToProductionAsync(string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFailProduction)
        {
            throw new InvalidOperationException("Falha simulada no deploy de produção.");
        }

        var id = $"prod-{Guid.NewGuid():N}";
        var result = new DeploymentResult(id, $"https://app.example.com/{id}", DeploymentStatus.Healthy);
        return Task.FromResult(result);
    }

    public Task<DeploymentResult> RollbackToPreviousAsync(string previousDeploymentId, CancellationToken cancellationToken = default)
    {
        var result = new DeploymentResult(previousDeploymentId, $"https://app.example.com/rollback-{previousDeploymentId}", DeploymentStatus.Healthy);
        return Task.FromResult(result);
    }
}
