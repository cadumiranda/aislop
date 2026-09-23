using System.Linq;
using AutonomiaSaaS.Modules.AgentRuntime.CostLogger;
using AutonomiaSaaS.Modules.AgentRuntime.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using YesSql.Indexes;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddAgentRuntimeInMemory_Registers_RuntimeServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "AgentRuntime:ApiKey", "dummy-key" },
            { "AgentRuntime:BaseUrl", "https://api.example" },
            { "AgentRuntime:TimeoutSeconds", "30" },
            { "AgentRuntime:ApiVersion", "2024-01-01" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        // Act
        services.AddAgentRuntimeInMemory(configuration);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IIndexProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IAnthropicClient));
        Assert.Contains(services, d => d.ServiceType == typeof(ICostLogger));
        Assert.Contains(services, d => d.ServiceType == typeof(IAgentRuntime));
    }
}