using AutonomiaSaaS.Modules.AcquisitionAgent;
using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Tests;

public class StartupTests
{
    [Fact]
    public void ConfigureServices_DefaultConfiguration_RegistersAcquisitionAgentOrchestrator()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var startup = new Startup(configuration);

        // Act
        startup.ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var orchestratorDescriptor = Assert.Single(serviceCollection, d => d.ServiceType == typeof(IAcquisitionAgentOrchestrator));
        Assert.Equal(ServiceLifetime.Scoped, orchestratorDescriptor.Lifetime);
        Assert.Equal(typeof(AcquisitionAgentOrchestrator), orchestratorDescriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureServices_DefaultConfiguration_RegistersIApprovedTaskExecutor()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var startup = new Startup(configuration);

        // Act
        startup.ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var orchestratorDescriptor = Assert.Single(serviceCollection, d => d.ServiceType == typeof(IApprovedTaskExecutor));
        Assert.Equal(ServiceLifetime.Scoped, orchestratorDescriptor.Lifetime);
        Assert.Equal(typeof(AcquisitionAgentApprovedTaskExecutor), orchestratorDescriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureServices_DefaultConfiguration_RegistersFakeDeploymentClient()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var startup = new Startup(configuration);

        // Act
        startup.ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var deploymentClient = serviceProvider.GetService<IDeploymentClient>();
        Assert.NotNull(deploymentClient);
        Assert.IsType<Services.FakeDeploymentClient>(deploymentClient);
    }

    [Fact]
    public void ConfigureServices_VercelUseFakeFalseAndApiTokenProvided_RegistersVercelDeploymentClient()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Vercel:UseFake", "false" },
            { "Vercel:ApiToken", "test-token-12345" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var startup = new Startup(configuration);

        // Act
        startup.ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var deploymentClient = serviceProvider.GetService<IDeploymentClient>();
        Assert.NotNull(deploymentClient);
        Assert.IsType<VercelDeploymentClient>(deploymentClient);
    }

    [Fact]
    public void ConfigureServices_VercelUseFakeFalseButTokenMissing_FallbackToFakeDeploymentClient()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Vercel:UseFake", "false" },
            { "Vercel:ApiToken", "" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var startup = new Startup(configuration);

        // Act
        startup.ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var deploymentClient = serviceProvider.GetService<IDeploymentClient>();
        Assert.NotNull(deploymentClient);
        Assert.IsType<Services.FakeDeploymentClient>(deploymentClient);
    }
}
