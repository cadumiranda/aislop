using System.Net;
using AutonomiaSaaS.Modules.AcquisitionAgent.Abstractions;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Tests;

public sealed class VercelDeploymentClientTests
{
    private static (VercelDeploymentClient Client, FakeHttpMessageHandler Handler) CreateClient(VercelApiOptions? options = null)
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        var client = new VercelDeploymentClient(
            httpClient,
            options ?? new VercelApiOptions
            {
                HealthCheckPollingInterval = TimeSpan.FromMilliseconds(5),
                HealthCheckTimeout = TimeSpan.FromMilliseconds(200),
            },
            NullLogger<VercelDeploymentClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task DeployToStagingAsync_PostsToV13Deployments_AndParsesResult()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, """{"id":"dpl_123","url":"my-app-abc.vercel.app","readyState":"QUEUED"}""");

        var result = await client.DeployToStagingAsync(new DeploymentRequest
        {
            ProjectId = "prj_abc",
            DeploymentName = "task-123-staging",
            Files = new Dictionary<string, string> { ["index.html"] = "<html></html>" },
        });

        Assert.Equal("dpl_123", result.DeploymentId);
        Assert.Equal(DeploymentReadyState.Queued, result.ReadyState);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/v13/deployments", request.RequestUri!.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public async Task DeployToStagingAsync_Throws_OnNonSuccessStatusCode()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.BadRequest, """{"error":"invalid_request"}""");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeployToStagingAsync(new DeploymentRequest
        {
            ProjectId = "prj_abc",
            DeploymentName = "task-123-staging",
            Files = new Dictionary<string, string> { ["index.html"] = "<html></html>" },
        }));
    }

    [Fact]
    public async Task CheckDeploymentAsync_ReturnsHealthy_WhenAlreadyReady()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, """{"id":"dpl_123","url":"my-app.vercel.app","readyState":"READY"}""");

        var result = await client.CheckDeploymentAsync("dpl_123");

        Assert.True(result.IsHealthy);
        Assert.Equal(DeploymentReadyState.Ready, result.ReadyState);
        Assert.False(result.TimedOut);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("/v13/deployments/dpl_123", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckDeploymentAsync_ReturnsUnhealthy_OnErrorState_WithoutPollingFurther()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, """{"id":"dpl_123","url":"","readyState":"ERROR","errorMessage":"build falhou"}""");

        var result = await client.CheckDeploymentAsync("dpl_123");

        Assert.False(result.IsHealthy);
        Assert.Equal(DeploymentReadyState.Error, result.ReadyState);
        Assert.Equal("build falhou", result.ErrorDetail);
        Assert.Single(handler.Requests); // não deveria continuar chamando depois de ERROR
    }

    [Fact]
    public async Task CheckDeploymentAsync_TimesOut_WhenNeverReachesReady()
    {
        var (client, handler) = CreateClient(new VercelApiOptions
        {
            HealthCheckPollingInterval = TimeSpan.FromMilliseconds(5),
            HealthCheckTimeout = TimeSpan.FromMilliseconds(30),
        });
        handler.Enqueue(HttpStatusCode.OK, """{"id":"dpl_123","url":"","readyState":"BUILDING"}""");

        var result = await client.CheckDeploymentAsync("dpl_123");

        Assert.False(result.IsHealthy);
        Assert.True(result.TimedOut);
        Assert.True(handler.Requests.Count > 1); // fez polling mais de uma vez antes de desistir
    }

    [Fact]
    public async Task PromoteToProductionAsync_PostsToCorrectPromoteUrl()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        await client.PromoteToProductionAsync("prj_abc", "dpl_123");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("/v10/projects/prj_abc/promote/dpl_123", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task RollbackToPreviousAsync_PostsToCorrectRollbackUrl()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        await client.RollbackToPreviousAsync("prj_abc", "dpl_old");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("/v1/projects/prj_abc/rollback/dpl_old", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task DeployToStagingAsync_IncludesTeamId_WhenConfigured()
    {
        var (client, handler) = CreateClient(new VercelApiOptions
        {
            TeamId = "team_xyz",
            HealthCheckPollingInterval = TimeSpan.FromMilliseconds(5),
            HealthCheckTimeout = TimeSpan.FromMilliseconds(200),
        });
        handler.Enqueue(HttpStatusCode.OK, """{"id":"dpl_123","url":"a.vercel.app","readyState":"QUEUED"}""");

        await client.DeployToStagingAsync(new DeploymentRequest
        {
            ProjectId = "prj_abc",
            DeploymentName = "task-123",
            Files = new Dictionary<string, string> { ["index.html"] = "<html></html>" },
        });

        var request = Assert.Single(handler.Requests);
        Assert.Contains("teamId=team_xyz", request.RequestUri!.Query);
    }

    [Fact]
    public async Task CheckDeploymentAsync_Throws_ForUnknownReadyState()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, """{"id":"dpl_123","url":"","readyState":"SOME_NEW_STATE_FROM_FUTURE_API"}""");

        // Nunca deveria tratar um estado desconhecido como "ainda rodando" silenciosamente.
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CheckDeploymentAsync("dpl_123"));
    }
}
