using AutonomiaSaaS.Modules.CredentialVault;
using AutonomiaSaaS.Modules.CredentialVault.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutonomiaSaaS.Modules.CredentialVault.Tests;

/// <summary>
/// Fake em memória para IAgentCredentialRecordStore — permite testar a lógica de
/// criptografia/rotação/revogação sem YesSql real. O que NÃO é coberto aqui:
/// YesSqlAgentCredentialRecordStore em si (ver README do módulo).
/// </summary>
public sealed class FakeAgentCredentialRecordStore : IAgentCredentialRecordStore
{
    private static readonly Dictionary<(string AgentName, string Key), AgentCredentialRecord> _records = new();

    public Task<AgentCredentialRecord?> FindAsync(string agentName, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.GetValueOrDefault((agentName, key)));

    public Task SaveAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default)
    {
        _records[(record.AgentName, record.Key)] = record;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AgentCredentialRecord record, CancellationToken cancellationToken = default)
    {
        _records.Remove((record.AgentName, record.Key));
        return Task.CompletedTask;
    }

    /// <summary>Só para o teste de "cross-agent" — simula um registro cujo AgentName foi alterado sem regravar o valor.</summary>
    public void OverwriteAgentNameDirectly(string oldAgentName, string key, string newAgentName)
    {
        var record = _records[(oldAgentName, key)];
        _records.Remove((oldAgentName, key));
        record.AgentName = newAgentName;
        _records[(newAgentName, key)] = record;
    }
}

public sealed class DataProtectionCredentialVaultTests : IDisposable
{
    private readonly string _keyRingDirectory =
        Path.Combine(Path.GetTempPath(), "credential-vault-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_keyRingDirectory))
        {
            Directory.Delete(_keyRingDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Provider real do Data Protection, com chaves escritas num diretório temporário exclusivo
    /// deste teste (removido no Dispose) — evita depender de uma classe efêmera cuja visibilidade
    /// pública eu não conseguia confirmar com certeza, e ainda assim não persiste nada além da
    /// duração do teste.
    /// </summary>
    private IDataProtectionProvider CreateTestProvider()
        => DataProtectionProvider.Create(new DirectoryInfo(_keyRingDirectory));

    private DataProtectionCredentialVault CreateVault(
        FakeAgentCredentialRecordStore store, IDataProtectionProvider? provider = null)
        => new(store, provider ?? CreateTestProvider(), NullLogger<DataProtectionCredentialVault>.Instance);

    [Fact]
    public async Task StoreAndTryGet_RoundTrips_ThePlaintextValue()
    {
        var store = new FakeAgentCredentialRecordStore();
        var vault = CreateVault(store);

        await vault.StoreAsync("acquisition_agent", "vercel_deploy_token", "segredo-123");
        var result = await vault.TryGetAsync("acquisition_agent", "vercel_deploy_token");

        Assert.Equal("segredo-123", result);
    }

    [Fact]
    public async Task StoreAsync_NeverPersistsThePlaintextValue()
    {
        var store = new FakeAgentCredentialRecordStore();
        var vault = CreateVault(store);

        await vault.StoreAsync("acquisition_agent", "vercel_deploy_token", "segredo-123");
        var record = await store.FindAsync("acquisition_agent", "vercel_deploy_token");

        Assert.NotNull(record);
        Assert.DoesNotContain("segredo-123", record!.ProtectedValue);
    }

    [Fact]
    public async Task TryGetAsync_ReturnsNull_WhenCredentialDoesNotExist()
    {
        var store = new FakeAgentCredentialRecordStore();
        var vault = CreateVault(store);

        var result = await vault.TryGetAsync("acquisition_agent", "nao_existe");

        Assert.Null(result);
    }

    [Fact]
    public async Task StoreAsync_CalledTwice_RotatesInPlace_InsteadOfDuplicating()
    {
        var store = new FakeAgentCredentialRecordStore();
        var vault = CreateVault(store);

        await vault.StoreAsync("acquisition_agent", "vercel_deploy_token", "valor-antigo");
        await vault.StoreAsync("acquisition_agent", "vercel_deploy_token", "valor-novo");

        var result = await vault.TryGetAsync("acquisition_agent", "vercel_deploy_token");
        var record = await store.FindAsync("acquisition_agent", "vercel_deploy_token");

        Assert.Equal("valor-novo", result);
        Assert.NotNull(record!.RotatedUtc);
    }

    [Fact]
    public async Task RevokeAsync_RemovesTheCredential()
    {
        var store = new FakeAgentCredentialRecordStore();
        var vault = CreateVault(store);
        await vault.StoreAsync("acquisition_agent", "vercel_deploy_token", "segredo-123");

        await vault.RevokeAsync("acquisition_agent", "vercel_deploy_token");
        var result = await vault.TryGetAsync("acquisition_agent", "vercel_deploy_token");

        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_IsIdempotent_ForANonExistentCredential()
    {
        var store = new FakeAgentCredentialRecordStore();
        var vault = CreateVault(store);

        // Não deve lançar.
        await vault.RevokeAsync("acquisition_agent", "nao_existe");
    }

    [Fact]
    public async Task DifferentAgents_CannotDecryptEachOthersCredentials_EvenWithSameKeyName()
    {
        var store = new FakeAgentCredentialRecordStore();
        var provider = CreateTestProvider(); // mesmo provider físico para os dois agentes
        var vault = CreateVault(store, provider);

        await vault.StoreAsync("acquisition_agent", "api_token", "segredo-do-acquisition");
        await vault.StoreAsync("ads_agent", "api_token", "segredo-do-ads");

        var acquisitionValue = await vault.TryGetAsync("acquisition_agent", "api_token");
        var adsValue = await vault.TryGetAsync("ads_agent", "api_token");

        Assert.Equal("segredo-do-acquisition", acquisitionValue);
        Assert.Equal("segredo-do-ads", adsValue);
    }

    [Fact]
    public async Task TryGetAsync_ReturnsNull_WhenRecordAgentNameWasChangedWithoutReEncrypting()
    {
        // Simula o cenário de defesa descrito no README: se o AgentName do registro for
        // alterado sem regravar o valor cifrado, a purpose do protetor não bate mais e a
        // decifragem deve falhar de forma controlada (null), não devolver lixo nem lançar
        // uma exceção não tratada para o chamador.
        var store = new FakeAgentCredentialRecordStore();
        var provider = CreateTestProvider();
        var vault = CreateVault(store, provider);

        await vault.StoreAsync("acquisition_agent", "api_token", "segredo-123");
        store.OverwriteAgentNameDirectly("acquisition_agent", "api_token", "ads_agent");

        var result = await vault.TryGetAsync("ads_agent", "api_token");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task StoreAsync_Throws_ForInvalidPlaintextValue(string? invalidValue)
    {
        var store = new FakeAgentCredentialRecordStore();
        var vault = CreateVault(store);

        if (invalidValue is null)
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => vault.StoreAsync("acquisition_agent", "api_token", invalidValue!));
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => vault.StoreAsync("acquisition_agent", "api_token", invalidValue!));
        }
    }
}
