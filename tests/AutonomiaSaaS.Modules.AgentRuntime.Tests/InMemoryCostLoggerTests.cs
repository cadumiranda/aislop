using AutonomiaSaaS.Modules.AgentRuntime.Cost;
using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;
using Xunit;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

public class InMemoryCostLoggerTests
{
    [Fact]
    public async Task LogAsync_ChamadasMultiplasMesmaTarefa_AcumulaTokens()
    {
        var logger = new InMemoryCostLogger();
        var agora = DateTimeOffset.UtcNow;

        await logger.LogAsync(new ModelCallCost(1L, "tenant_a", "tenant_a", "claude-sonnet-5", ActivityComplexity.Planning, 100, 50, agora));
        await logger.LogAsync(new ModelCallCost(1L, "tenant_a", "tenant_a", "claude-sonnet-5", ActivityComplexity.Planning, 30, 10, agora));

        var total = await logger.GetAccumulatedTokensForTaskAsync(1L);

        Assert.Equal(190, total); // (100+50) + (30+10)
    }

    [Fact]
    public async Task GetAccumulatedTokensForTaskAsync_TarefaDiferente_NaoMisturaCustos()
    {
        var logger = new InMemoryCostLogger();
        var agora = DateTimeOffset.UtcNow;

        await logger.LogAsync(new ModelCallCost(1L, "tenant_a", "tenant_a", "claude-sonnet-5", ActivityComplexity.Planning, 100, 50, agora));
        await logger.LogAsync(new ModelCallCost(2L, "tenant_a", "tenant_a", "claude-sonnet-5", ActivityComplexity.Planning, 999, 999, agora));

        var totalTask1 = await logger.GetAccumulatedTokensForTaskAsync(1L);

        Assert.Equal(150, totalTask1);
    }

    [Fact]
    public async Task GetAccumulatedTokensForTaskAsync_TarefaSemRegistro_RetornaZero()
    {
        var logger = new InMemoryCostLogger();

        var total = await logger.GetAccumulatedTokensForTaskAsync(0L);

        Assert.Equal(0, total);
    }
}
