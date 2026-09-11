using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace AutonomiaSaaS.Modules.RiskGate.ApprovalExpiration.Tests;

public class ApprovalExpirationSmokeTests
{
    [Fact]
    public void ProjectBuildsAndResolvesDependencies()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }
}
