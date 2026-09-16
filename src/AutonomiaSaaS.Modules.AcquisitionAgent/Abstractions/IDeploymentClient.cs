namespace AutonomiaSaaS.Modules.AcquisitionAgent.Abstractions;

// ============================================================================
// APAGUE ESTE ARQUIVO ao integrar com o AutonomiaSaaS.Modules.AcquisitionAgent real.
// IDeploymentClient já existe (spec v2.0, seção 9.1, e LocalFakeDeploymentClient já o
// implementa) — esta é uma assinatura INFERIDA. Ajuste VercelDeploymentClient pela real.
// ============================================================================

public enum DeploymentReadyState
{
    Queued,
    Initializing,
    Building,
    Ready,
    Error,
    Canceled,
}

public sealed record DeploymentRequest
{
    /// <summary>Identificador do projeto na Vercel — não o TaskId do AgentTask.</summary>
    public required string ProjectId { get; init; }

    /// <summary>Nome do deployment (aparece na UI da Vercel) — sugerido: derivado do TaskId para rastreabilidade.</summary>
    public required string DeploymentName { get; init; }

    /// <summary>Caminho relativo -> conteúdo do arquivo. Para landing page de Fase 1, tipicamente só "index.html".</summary>
    public required IReadOnlyDictionary<string, string> Files { get; init; }
}

public sealed record DeploymentResult
{
    public required string DeploymentId { get; init; }
    public required string Url { get; init; }
    public required DeploymentReadyState ReadyState { get; init; }
}

public sealed record DeploymentHealthResult
{
    public required bool IsHealthy { get; init; }
    public required DeploymentReadyState ReadyState { get; init; }
    public string? Url { get; init; }
    public bool TimedOut { get; init; }
    public string? ErrorDetail { get; init; }
}

public interface IDeploymentClient
{
    Task<DeploymentResult> DeployToStagingAsync(DeploymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Faz polling até READY/ERROR/CANCELED ou timeout — ver README para os valores default.</summary>
    Task<DeploymentHealthResult> CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default);

    /// <summary>Aponta o tráfego de produção do projeto para este deployment. Não refaz o build (comportamento nativo da Vercel).</summary>
    Task PromoteToProductionAsync(string projectId, string stagingDeploymentId, CancellationToken cancellationToken = default);

    /// <summary>Reverte produção para um deployment anterior específico — quem chama precisa já saber qual era o deployment de produção anterior.</summary>
    Task RollbackToPreviousAsync(string projectId, string previousProductionDeploymentId, CancellationToken cancellationToken = default);
}
