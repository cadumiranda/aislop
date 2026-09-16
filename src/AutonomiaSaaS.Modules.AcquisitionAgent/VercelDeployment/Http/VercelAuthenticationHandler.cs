using System.Net.Http.Headers;
using AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Configuration;
using AutonomiaSaaS.Modules.CredentialVault.Abstractions;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Http;

/// <summary>
/// Busca o token no cofre a cada requisição (não uma vez no startup) — se o token for
/// rotacionado no cofre, a próxima chamada já usa o novo, sem precisar reiniciar o processo.
/// Falha alto e imediato se o token não estiver disponível: nunca deixa a requisição sair sem
/// Authorization, o que só resultaria numa 401 confusa vinda da Vercel.
/// </summary>
public sealed class VercelAuthenticationHandler : DelegatingHandler
{
    private readonly ICredentialVault _credentialVault;
    private readonly VercelApiOptions _options;

    public VercelAuthenticationHandler(ICredentialVault credentialVault, VercelApiOptions options)
    {
        _credentialVault = credentialVault;
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _credentialVault.TryGetAsync(
            _options.CredentialAgentName, _options.CredentialKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"Token da Vercel não encontrado no cofre (agente '{_options.CredentialAgentName}', " +
                $"chave '{_options.CredentialKey}'). Cadastre via /Admin/AgentCredentialsAdmin antes " +
                "de usar o VercelDeploymentClient.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
