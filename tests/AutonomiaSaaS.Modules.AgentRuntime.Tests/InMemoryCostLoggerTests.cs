using AutonomiaSaaS.Modules.AgentRuntime.Cost;
using Xunit;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

public class InMemoryCostLoggerTests
{
    [Fact]
    public async Task LogAsync_ChamadasMultiplasMesmaTarefa_AcumulaTokens()
    {
        var logger = new InMemoryCostLogger();
        var agora = DateTimeOffset.UtcNow;

        await logger.LogAsync(new ModelCallCost("task_1", "tenant_a", "claude-sonnet-5", 100, 50, agora));
        await logger.LogAsync(new ModelCallCost("task_1", "tenant_a", "claude-sonnet-5", 30, 10, agora));

        var total = await logger.GetAccumulatedTokensForTaskAsync("task_1");

        Assert.Equal(190, total); // (100+50) + (30+10)
    }

    [Fact]
    public async Task GetAccumulatedTokensForTaskAsync_TarefaDiferente_NaoMisturaCustos()
    {
        var logger = new InMemoryCostLogger();
        var agora = DateTimeOffset.UtcNow;

        await logger.LogAsync(new ModelCallCost("task_1", "tenant_a", "claude-sonnet-5", 100, 50, agora));
        await logger.LogAsync(new ModelCallCost("task_2", "tenant_a", "claude-sonnet-5", 999, 999, agora));

        var totalTask1 = await logger.GetAccumulatedTokensForTaskAsync("task_1");

        Assert.Equal(150, totalTask1);
    }

    [Fact]
    public async Task GetAccumulatedTokensForTaskAsync_TarefaSemRegistro_RetornaZero()
    {
        var logger = new InMemoryCostLogger();

        var total = await logger.GetAccumulatedTokensForTaskAsync("task_inexistente");

        Assert.Equal(0, total);
    }
}
