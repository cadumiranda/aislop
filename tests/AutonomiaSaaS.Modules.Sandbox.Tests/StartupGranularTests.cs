using System;
using System.Reflection;
using AutonomiaSaaS.Modules.Sandbox;
using AutonomiaSaaS.Modules.Sandbox.Abstractions;
using AutonomiaSaaS.Modules.Sandbox.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AutonomiaSaaS.Modules.Sandbox.Tests;

public class StartupGranularTests
{
    [Fact]
    public void ConfigureServices_Binds_DockerSandboxOptions_FromConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemory = new System.Collections.Generic.Dictionary<string, string?>
        {
            { "Sandbox:Docker:WorkspaceRootPath", "C:\\workspaces" },
            { "Sandbox:Docker:DockerExecutablePath", "/usr/bin/docker" },
            { "Sandbox:Docker:RetainWorkspaceOnFailureForDebugging", "false" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        services.AddSingleton<IConfiguration>(configuration);
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<DockerSandboxOptions>();
        Assert.NotNull(options);
        Assert.Equal("C:\\workspaces", options.WorkspaceRootPath);
        Assert.Equal("/usr/bin/docker", options.DockerExecutablePath);
        Assert.False(options.RetainWorkspaceOnFailureForDebugging);
    }

    [Fact]
    public void ConfigureServices_Resolves_ISandboxRunner_As_DockerSandboxRunner_With_Injections()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemory = new System.Collections.Generic.Dictionary<string, string?>
        {
            { "Sandbox:Docker:WorkspaceRootPath", "C:\\workspaces" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add logging so ILogger<T> can be resolved by the DI container
        services.AddLogging();

        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        // Assert: ISandboxRunner resolves and is the concrete DockerSandboxRunner
        var runner = provider.GetService<ISandboxRunner>();
        Assert.NotNull(runner);
        Assert.IsType<DockerSandboxRunner>(runner);

        // Ensure the same DockerSandboxOptions instance is injected into the runner
        var options = provider.GetRequiredService<DockerSandboxOptions>();
        // use reflection to peek the private field _options
        var optionsField = runner.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(optionsField);
        var injectedOptions = optionsField!.GetValue(runner);
        Assert.Same(options, injectedOptions);
    }

    [Fact]
    public void ConfigureServices_Registers_Singleton_Implementations_For_ProcessInvoker_And_Queue()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        // Resolve twice and ensure singleton semantics
        var invoker1 = provider.GetService<IProcessInvoker>();
        var invoker2 = provider.GetService<IProcessInvoker>();
        Assert.NotNull(invoker1);
        Assert.Same(invoker1, invoker2);

        var queue1 = provider.GetService<ISandboxExecutionQueue>();
        var queue2 = provider.GetService<ISandboxExecutionQueue>();
        Assert.NotNull(queue1);
        Assert.Same(queue1, queue2);
    }

    [Fact]
    public void ConfigureServices_Registers_HostedService_SandboxExecutionHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert there is a registration of IHostedService with implementation SandboxExecutionHostedService
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Name == "SandboxExecutionHostedService");
    }
}
