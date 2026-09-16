using AutonomiaSaaS.Modules.AcquisitionAgent.Abstractions;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Configuration;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Tests.IntegrationTests;

/// <summary>
/// ESTES TESTES FAZEM CHAMADAS REAIS À API DA VERCEL. Não rodam em CI nem por padrão — ver
/// README, "aviso mais importante": eu não consigo executá-los deste ambiente (rede restrita).
///
/// Para rodar você mesmo:
///   1. Crie um projeto de teste na Vercel, isolado de produção (passo 1 do plano original).
///   2. Gere um token de API só para esse teste.
///   3. Defina as variáveis de ambiente abaixo e rode `dotnet test --filter Integration`.
///   4. Confira manualmente no dashboard da Vercel que o deployment de staging realmente
///      apareceu, foi promovido, e o rollback realmente reverteu — não confie só no teste
///      passar verde, porque o schema de resposta pode ter sido mal interpretado mesmo com
///      status HTTP de sucesso (ver aviso sobre schema inferido no README).
///
///   VERCEL_TEST_TOKEN=<token do projeto de teste>
///   VERCEL_TEST_PROJECT_ID=<id do projeto de teste>
/// </summary>
public sealed class VercelDeploymentClientIntegrationTests
{
    private static bool HasCredentials =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VERCEL_TEST_TOKEN")) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VERCEL_TEST_PROJECT_ID"));

    private static VercelDeploymentClient CreateRealClient()
    {
        var token = Environment.GetEnvironmentVariable("VERCEL_TEST_TOKEN")!;
        var handler = new StaticBearerTokenHandler(token) { InnerHandler = new HttpClientHandler() };
        var httpClient = new HttpClient(handler);
        var options = new VercelApiOptions
        {
            HealthCheckTimeout = TimeSpan.FromMinutes(3),
            HealthCheckPollingInterval = TimeSpan.FromSeconds(3),
        };
        return new VercelDeploymentClient(httpClient, options, NullLogger<VercelDeploymentClient>.Instance);
    }

    [Fact(Skip = "Requer VERCEL_TEST_TOKEN e VERCEL_TEST_PROJECT_ID no ambiente — ver comentário da classe. Rode manualmente.")]
    public async Task FullCycle_DeployStagingCheckPromoteRollback_AgainstRealVercelTestProject()
    {
        if (!HasCredentials)
        {
            return; // Skip já cobre isso, mas é uma segunda trava caso o Skip seja removido sem querer.
        }

        var projectId = Environment.GetEnvironmentVariable("VERCEL_TEST_PROJECT_ID")!;
        var client = CreateRealClient();

        // 1. Deploy de staging.
        var deployment = await client.DeployToStagingAsync(new DeploymentRequest
        {
            ProjectId = projectId,
            DeploymentName = $"integration-test-{Guid.NewGuid():N}",
            Files = new Dictionary<string, string>
            {
                ["index.html"] = "<html><body><h1>Teste de integração AutonomiaSaaS</h1></body></html>",
            },
        });
        Assert.NotEmpty(deployment.DeploymentId);

        // 2. Checar saúde — deve ficar READY dentro do timeout configurado.
        var health = await client.CheckDeploymentAsync(deployment.DeploymentId);
        Assert.True(health.IsHealthy, $"Deployment não ficou saudável: {health.ErrorDetail}");

        // 3. Promover para produção.
        await client.PromoteToProductionAsync(projectId, deployment.DeploymentId);

        // 4. Forçar rollback de propósito, para validar que o endpoint de rollback funciona
        //    (revertendo para o próprio deployment recém-promovido, já que é o único conhecido
        //    neste teste isolado — num teste mais completo, seria o deployment de produção
        //    anterior a este).
        await client.RollbackToPreviousAsync(projectId, deployment.DeploymentId);

        // Nenhum assert automático além dos IsHealthy acima — a instrução no comentário da
        // classe pede conferência manual no dashboard da Vercel, porque um 2xx não garante que
        // o schema foi interpretado do jeito certo (ver aviso no README).
    }

    private sealed class StaticBearerTokenHandler : DelegatingHandler
    {
        private readonly string _token;
        public StaticBearerTokenHandler(string token) => _token = token;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
