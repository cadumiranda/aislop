using System.Net.Http.Json;
using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Services;

/// <summary>
/// Implementação real de IDeploymentClient sobre a API da Vercel.
///
/// AVISO: esta é a parte do módulo com MENOS confiança de estar correta.
/// Não confirmei os endpoints exatos da Vercel Deployments API (formato de
/// payload, nomes de campo, endpoint de promoção de produção) — escrevi uma
/// estrutura plausível baseada no padrão REST usual da Vercel, mas isso
/// precisa ser validado contra a documentação oficial atual antes de ir para
/// produção. Trate como esqueleto de integração, não como implementação
/// pronta. Os testes do orquestrador não dependem desta classe — usam
/// IDeploymentClient fake — então isso não bloqueia validar o resto do
/// módulo.
/// </summary>
public sealed class VercelDeploymentClient : IDeploymentClient
{
    private readonly HttpClient _httpClient;

    public VercelDeploymentClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DeploymentResult> DeployToStagingAsync(
        string html, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/v13/deployments",
            new { name = "acquisition-agent-staging", files = new[] { new { file = "index.html", data = html } }, target = "staging" },
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<VercelDeploymentResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta vazia da Vercel ao criar deployment de staging.");

        return new DeploymentResult(body.Id, body.Url, DeploymentStatus.Healthy);
    }

    public async Task<DeploymentStatus> CheckDeploymentAsync(
        string deploymentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient
            .GetAsync($"/v13/deployments/{deploymentId}", cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return DeploymentStatus.Unhealthy;
        }

        var body = await response.Content
            .ReadFromJsonAsync<VercelDeploymentResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return body?.ReadyState == "READY" ? DeploymentStatus.Healthy : DeploymentStatus.Unhealthy;
    }

    public async Task<DeploymentResult> PromoteToProductionAsync(
        string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"/v13/deployments/{stagingDeploymentId}/promote", content: null, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<VercelDeploymentResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta vazia da Vercel ao promover para produção.");

        return new DeploymentResult(){ DeploymentId: body.Id, Url: body.Url, Status: DeploymentStatus.Healthy, ReadyState: body.ReadyState };
    }

    public async Task<DeploymentResult> RollbackToPreviousAsync(
        string previousDeploymentId, CancellationToken cancellationToken = default)
        // Rollback na Vercel é, na prática, promover de novo o deployment
        // anterior — não existe uma operação distinta de "desfazer".
        => await PromoteToProductionAsync(previousDeploymentId, cancellationToken).ConfigureAwait(false);

    public Task<DeploymentResult> DeployToStagingAsync(DeploymentRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    Task<DeploymentHealthResult> IDeploymentClient.CheckDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<DeploymentResult> PromoteToProductionAsync(string projectId, string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        return await PromoteToProductionAsync(stagingDeploymentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeploymentResult> RollbackToPreviousAsync(string projectId, string previousProductionDeploymentId, CancellationToken cancellationToken = default)
    {
        return await RollbackToPreviousAsync(previousProductionDeploymentId, cancellationToken).ConfigureAwait(false);
    }
}
