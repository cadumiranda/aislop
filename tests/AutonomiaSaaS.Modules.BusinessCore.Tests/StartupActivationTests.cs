using System;
using AutonomiaSaaS.Modules.BusinessCore;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutonomiaSaaS.Modules.BusinessCore.Tests;

public class StartupActivationTests
{
    [Fact]
    public void ConfigureServices_When_Built_Provider_Resolving_IAgentTaskStore_Throws_Missing_Dependency()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new Startup();

        // Act
        startup.ConfigureServices(services);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        // Assert: resolving IAgentTaskStore should fail because required infrastructure (YesSql.ISession, OrchardCore services)
        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IAgentTaskStore>());

        // Make the assertion explicit about the likely missing dependency
        Assert.Contains("Unable to resolve service for type 'OrchardCore.", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
