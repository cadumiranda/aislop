using System.Linq;
using AutonomiaSaaS.Modules.CredentialVault;
using AutonomiaSaaS.Modules.CredentialVault.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutonomiaSaaS.Modules.CredentialVault.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void Startup_Registers_CredentialVaultServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(YesSql.Indexes.IIndexProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IAgentCredentialRecordStore));
        Assert.Contains(services, d => d.ServiceType == typeof(AutonomiaSaaS.Modules.CredentialVault.Abstractions.ICredentialVault));
        Assert.Contains(services, d => d.ServiceType == typeof(OrchardCore.Navigation.INavigationProvider));
    }
}
