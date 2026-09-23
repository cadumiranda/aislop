using OrchardCore.Modules;
using System.Text.Json.Serialization;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Http;

// ============================================================================
// Schema PARCIALMENTE INFERIDO — ver README, "segundo aviso". Valide contra uma resposta
// real antes de confiar nisto em produção. Os nomes de campo (name, files, target, id, url,
// readyState) foram confirmados via busca na documentação oficial; a forma exata de aninhamento
// pode precisar de ajuste.
// ============================================================================
[Feature("AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment")]
internal sealed class VercelDeploymentFile
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

[Feature("AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment")]
internal sealed class CreateDeploymentRequestBody
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("files")]
    public required List<VercelDeploymentFile> Files { get; init; }

    /// <summary>
    /// Omitido (null) para preview/staging — só definido como "production" quando se quer pular
    /// o fluxo de staging inteiramente, o que este cliente nunca faz de propósito (a Fase 1
    /// sempre passa por staging + aprovação antes de qualquer produção).
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; init; }
}

[Feature("AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment")]
internal sealed class DeploymentResponseBody
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("readyState")]
    public required string ReadyState { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
