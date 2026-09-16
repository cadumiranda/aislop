using System.Net;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Configuration;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Http;
using Xunit;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Tests;

/// <summary>Handler de teste que expõe SendAsync publicamente via um invoker HttpClient.</summary>
public sealed class VercelAuthenticationHandlerTests
{
    private static (HttpClient Client, FakeHttpMessageHandler Inner, FakeCredentialVaultForVercelTests Vault) CreatePipeline(
        VercelApiOptions? options = null)
    {
        var vault = new FakeCredentialVaultForVercelTests();
        var innerHandler = new FakeHttpMessageHandler();
        var authHandler = new VercelAuthenticationHandler(vault, options ?? new VercelApiOptions())
        {
            InnerHandler = innerHandler,
        };
        var client = new HttpClient(authHandler);
        return (client, innerHandler, vault);
    }

    [Fact]
    public async Task SendAsync_AttachesBearerToken_FetchedFromVault_PerRequest()
    {
        var (client, inner, vault) = CreatePipeline();
        vault.Values[("acquisition_agent", "vercel_deploy_token")] = "token-123";
        inner.Enqueue(HttpStatusCode.OK, "{}");

        await client.GetAsync("https://api.vercel.com/v13/deployments/dpl_1");

        var request = Assert.Single(inner.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token-123", request.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task SendAsync_Throws_WhenTokenNotFoundInVault_WithoutMakingTheRequest()
    {
        var (client, inner, _) = CreatePipeline(); // cofre vazio, nenhum token gravado

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("https://api.vercel.com/v13/deployments/dpl_1"));

        // A garantia central deste handler: falha antes de sair qualquer requisição sem token.
        Assert.Empty(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_UsesConfiguredAgentNameAndKey_NotHardcodedDefaults()
    {
        var options = new VercelApiOptions
        {
            CredentialAgentName = "outro_agente",
            CredentialKey = "outra_chave",
        };
        var (client, inner, vault) = CreatePipeline(options);
        vault.Values[("outro_agente", "outra_chave")] = "token-customizado";
        inner.Enqueue(HttpStatusCode.OK, "{}");

        await client.GetAsync("https://api.vercel.com/v13/deployments/dpl_1");

        var request = Assert.Single(inner.Requests);
        Assert.Equal("token-customizado", request.Headers.Authorization!.Parameter);
    }
}
