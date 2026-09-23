using System.Net.Http.Json;
using AutonomiaSaaS.Modules.AcquisitionAgent.Abstractions;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Configuration;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment;

/// <summary>
/// Ver README para os avisos completos — em especial, isto nunca foi executado contra a Vercel
/// real por mim (restrição de rede deste ambiente). Testado com HttpMessageHandler fake.
/// </summary>
[Feature("AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment")]
public sealed class VercelDeploymentClient : IDeploymentClient
{
    private readonly HttpClient _httpClient;
    private readonly VercelApiOptions _options;
    private readonly ILogger<VercelDeploymentClient> _logger;

    public VercelDeploymentClient(HttpClient httpClient, VercelApiOptions options, ILogger<VercelDeploymentClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<DeploymentResult> DeployToStagingAsync(
        DeploymentRequest request, CancellationToken cancellationToken = default)
    {
        var body = new CreateDeploymentRequestBody
        {
            Name = request.DeploymentName,
            Files = request.Files
                .Select(f => new VercelDeploymentFile { File = f.Key, Data = f.Value })
                .ToList(),
            Target = null, // preview/staging — nunca "production" direto (ver comentário no DTO)
        };

        var url = BuildUrl("/v13/deployments");
        _logger.LogInformation("Vercel: criando deployment de staging '{Name}'.", request.DeploymentName);

        using var response = await _httpClient.PostAsJsonAsync(url, body, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, "criar deployment de staging", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<DeploymentResponseBody>(cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia da Vercel ao criar deployment.");

        return new DeploymentResult
        {
            DeploymentId = payload.Id,
            Url = payload.Url,
            ReadyState = ParseReadyState(payload.ReadyState),
        };
    }

    public async Task<DeploymentHealthResult> CheckDeploymentAsync(
        string deploymentId, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + _options.HealthCheckTimeout;

        while (true)
        {
            var url = BuildUrl($"/v13/deployments/{Uri.EscapeDataString(deploymentId)}");
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            await ThrowIfUnsuccessfulAsync(response, "consultar status do deployment", cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<DeploymentResponseBody>(cancellationToken)
                ?? throw new InvalidOperationException("Resposta vazia da Vercel ao consultar deployment.");

            var state = ParseReadyState(payload.ReadyState);

            if (state == DeploymentReadyState.Ready)
            {
                return new DeploymentHealthResult { IsHealthy = true, ReadyState = state, Url = payload.Url };
            }

            if (state is DeploymentReadyState.Error or DeploymentReadyState.Canceled)
            {
                _logger.LogWarning(
                    "Vercel: deployment {DeploymentId} terminou em {State}: {ErrorDetail}",
                    deploymentId, state, payload.ErrorMessage);
                return new DeploymentHealthResult
                {
                    IsHealthy = false,
                    ReadyState = state,
                    ErrorDetail = payload.ErrorMessage,
                };
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                _logger.LogWarning(
                    "Vercel: timeout aguardando deployment {DeploymentId} ficar pronto (último estado: {State}).",
                    deploymentId, state);
                return new DeploymentHealthResult { IsHealthy = false, ReadyState = state, TimedOut = true };
            }

            await Task.Delay(_options.HealthCheckPollingInterval, cancellationToken);
        }
    }

    public async Task PromoteToProductionAsync(
        string projectId, string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"/v10/projects/{Uri.EscapeDataString(projectId)}/promote/{Uri.EscapeDataString(stagingDeploymentId)}");
        _logger.LogInformation(
            "Vercel: promovendo deployment {DeploymentId} para produção no projeto {ProjectId}.",
            stagingDeploymentId, projectId);

        using var response = await _httpClient.PostAsync(url, content: null, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, "promover deployment para produção", cancellationToken);
    }

    public async Task RollbackToPreviousAsync(
        string projectId, string previousProductionDeploymentId, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"/v1/projects/{Uri.EscapeDataString(projectId)}/rollback/{Uri.EscapeDataString(previousProductionDeploymentId)}");
        _logger.LogWarning(
            "Vercel: revertendo produção do projeto {ProjectId} para o deployment anterior {DeploymentId}.",
            projectId, previousProductionDeploymentId);

        using var response = await _httpClient.PostAsync(url, content: null, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, "reverter deployment de produção", cancellationToken);
    }

    private string BuildUrl(string path)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}{path}";
        return string.IsNullOrEmpty(_options.TeamId) ? url : $"{url}?teamId={Uri.EscapeDataString(_options.TeamId)}";
    }

    private static DeploymentReadyState ParseReadyState(string readyState) => readyState.ToUpperInvariant() switch
    {
        "QUEUED" => DeploymentReadyState.Queued,
        "INITIALIZING" => DeploymentReadyState.Initializing,
        "BUILDING" => DeploymentReadyState.Building,
        "READY" => DeploymentReadyState.Ready,
        "ERROR" => DeploymentReadyState.Error,
        "CANCELED" => DeploymentReadyState.Canceled,
        // Falha alto em vez de assumir um estado — um valor inesperado da Vercel (API mudou?)
        // não deveria ser silenciosamente tratado como "ainda rodando" ou "deu certo".
        _ => throw new InvalidOperationException($"readyState desconhecido retornado pela Vercel: '{readyState}'."),
    };

    private static async Task ThrowIfUnsuccessfulAsync(
        HttpResponseMessage response, string operationDescription, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Falha ao {operationDescription} na Vercel: {(int)response.StatusCode} {response.ReasonPhrase}. " +
            $"Corpo da resposta: {body}");
    }
}
