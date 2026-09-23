using System.Linq;
using AutonomiaSaaS.Modules.RiskGate;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using AutonomiaSaaS.Modules.RiskGate.Services;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void Startup_Registers_RiskGateServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IAgentTaskStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IRiskClassifier));
        Assert.Contains(services, d => d.ServiceType == typeof(ITaskRiskGateway));
        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) || d.ServiceType == typeof(OrchardCore.BackgroundTasks.IBackgroundTask));
        Assert.Contains(services, d => d.ServiceType == typeof(OrchardCore.Navigation.INavigationProvider));

        var agentTaskStoreDescriptor = services.Single(d => d.ServiceType == typeof(IAgentTaskStore));
        Assert.Equal(ServiceLifetime.Scoped, agentTaskStoreDescriptor.Lifetime);
    }
}
