using System.Linq;
using AutonomiaSaaS.Modules.BusinessCore;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutonomiaSaaS.Modules.BusinessCore.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void Startup_Registers_CoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IAgentTaskStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IBusinessContextStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IApprovedTaskDispatcher));
        Assert.Contains(services, d => d.ServiceType == typeof(YesSql.Indexes.IIndexProvider));

        var agentTaskStoreDescriptor = services.Single(d => d.ServiceType == typeof(IAgentTaskStore));
        Assert.Equal(ServiceLifetime.Scoped, agentTaskStoreDescriptor.Lifetime);
    }
}
