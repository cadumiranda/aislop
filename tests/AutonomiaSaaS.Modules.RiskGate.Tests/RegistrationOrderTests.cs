using System.Linq;
using AutonomiaSaaS.Modules.BusinessCore;
using AutonomiaSaaS.Modules.RiskGate;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

public class RegistrationOrderTests
{
    [Fact]
    public void BusinessCore_Then_RiskGate_Register_IAgentTaskStore()
    {
        var services = new ServiceCollection();

        // Simulate OrchardCore: BusinessCore startup runs first
        var bcStartup = new AutonomiaSaaS.Modules.BusinessCore.Startup();
        bcStartup.ConfigureServices(services);

        var rgStartup = new AutonomiaSaaS.Modules.RiskGate.Startup();
        rgStartup.ConfigureServices(services);

        // Assert that IAgentTaskStore has at least one registration
        var has = services.Any(d => d.ServiceType == typeof(IAgentTaskStore));
        Assert.True(has, "IAgentTaskStore should be registered when BusinessCore then RiskGate Startups run.");
    }

    [Fact]
    public void RiskGate_Then_BusinessCore_Register_IAgentTaskStore()
    {
        var services = new ServiceCollection();

        // RiskGate runs before BusinessCore (mis-ordered)
        var rgStartup = new AutonomiaSaaS.Modules.RiskGate.Startup();
        rgStartup.ConfigureServices(services);

        var bcStartup = new AutonomiaSaaS.Modules.BusinessCore.Startup();
        bcStartup.ConfigureServices(services);

        var has = services.Any(d => d.ServiceType == typeof(IAgentTaskStore));
        Assert.True(has, "IAgentTaskStore should be registered even if RiskGate startup runs before BusinessCore (RiskGate registers it too).");
    }

    [Fact]
    public void Combined_Startups_DoNot_Duplicate_Unexpectedly()
    {
        var services = new ServiceCollection();
        var bcStartup = new AutonomiaSaaS.Modules.BusinessCore.Startup();
        var rgStartup = new AutonomiaSaaS.Modules.RiskGate.Startup();

        bcStartup.ConfigureServices(services);
        rgStartup.ConfigureServices(services);

        var descriptors = services.Where(d => d.ServiceType == typeof(IAgentTaskStore)).ToList();
        Assert.NotEmpty(descriptors);
        // It's acceptable to have multiple registrations, but assert implementations are AgentTaskStore
        Assert.All(descriptors, d => Assert.True(d.ImplementationType?.Name == "AgentTaskStore" || d.ImplementationFactory != null || d.ImplementationInstance != null));
    }
}
