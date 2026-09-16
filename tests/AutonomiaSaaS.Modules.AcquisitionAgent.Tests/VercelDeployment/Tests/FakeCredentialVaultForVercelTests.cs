using AutonomiaSaaS.Modules.CredentialVault.Abstractions;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Tests;

public sealed class FakeCredentialVaultForVercelTests : ICredentialVault
{
    public Dictionary<(string AgentName, string Key), string> Values { get; } = new();

    public Task StoreAsync(string agentName, string key, string plaintextValue, string? description = null, CancellationToken cancellationToken = default)
    {
        Values[(agentName, key)] = plaintextValue;
        return Task.CompletedTask;
    }

    public Task<string?> TryGetAsync(string agentName, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(Values.GetValueOrDefault((agentName, key)));

    public Task RevokeAsync(string agentName, string key, CancellationToken cancellationToken = default)
    {
        Values.Remove((agentName, key));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CredentialSummary>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CredentialSummary>>(Array.Empty<CredentialSummary>());
}