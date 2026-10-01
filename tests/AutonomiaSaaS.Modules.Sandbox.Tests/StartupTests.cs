using System.Linq;
using AutonomiaSaaS.Modules.Sandbox;
using AutonomiaSaaS.Modules.Sandbox.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AutonomiaSaaS.Modules.Sandbox.Tests;

public class StartupTests
{
    [Fact]
    public void ConfigureServices_Registers_SandboxServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(DockerSandboxOptions));

        var processInvokerDescriptor = services.Single(d => d.ServiceType == typeof(IProcessInvoker));
        Assert.Equal(ServiceLifetime.Singleton, processInvokerDescriptor.Lifetime);

        var runnerDescriptor = services.Single(d => d.ServiceType == typeof(ISandboxRunner));
        Assert.Equal(ServiceLifetime.Singleton, runnerDescriptor.Lifetime);
        Assert.Equal(typeof(DockerSandboxRunner), runnerDescriptor.ImplementationType);

        Assert.Contains(services, d => d.ServiceType == typeof(ISandboxExecutionQueue) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(ISandboxExecutionResultSink) && d.Lifetime == ServiceLifetime.Singleton);

        // Hosted service registration exists
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Name == "SandboxExecutionHostedService");
    }
}
