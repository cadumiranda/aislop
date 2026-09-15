using AutonomiaSaaS.Modules.AgentRuntime.Cost;
using AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;
using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;
using AutonomiaSaaS.Modules.AgentRuntime.Pricing;
using Xunit;

namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Tests;

public sealed class FakeCostLogEntryStore : ICostLogEntryStore
{
    private readonly List<CostLogEntryRecord> _records = new();

    public Task AddAsync(CostLogEntryRecord record, CancellationToken cancellationToken = default)
    {
        _records.Add(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CostLogEntryRecord>> FindByTaskAsync(long taskId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CostLogEntryRecord>>(_records.Where(r => r.TaskId == taskId).ToList());

    public Task<IReadOnlyList<CostLogEntryRecord>> FindByAgentSinceAsync(
        string agentName, DateTimeOffset since, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CostLogEntryRecord>>(
            _records.Where(r => r.AgentName == agentName && r.CreatedUtc >= since.UtcDateTime).ToList());
}

public sealed class PersistentCostLoggerTests
{
    private static IModelPricingProvider CreateFixedPricingProvider()
        => new StaticModelPricingCatalog(new Dictionary<string, ModelRate>
        {
            ["test-model"] = new ModelRate { InputPerMillionTokens = 2m, OutputPerMillionTokens = 10m },
        });

    private static PersistentCostLogger CreateLogger(FakeCostLogEntryStore store, IModelPricingProvider? pricing = null)
        => new(store, pricing ?? CreateFixedPricingProvider());

    [Fact]
    public async Task LogAsync_ComputesCost_UsingRateTable()
    {
        var store = new FakeCostLogEntryStore();
        var logger = CreateLogger(store);

        // 1_000_000 tokens de input a $2/MTok = $2,00; 500_000 tokens de output a $10/MTok = $5,00
        await logger.LogAsync(new ModelCallCost(TaskId: 1L, AgentName: "acquisition_agent", TenantId: "tenant1", Model: "test-model", Complexity: ActivityComplexity.Planning, InputTokens: 1_000_000, OutputTokens: 500_000, OccurredAt: DateTimeOffset.UtcNow));

        var total = await logger.GetTotalCostForTaskAsync(1L);

        Assert.Equal(7.00m, total);
    }

    [Fact]
    public async Task GetTotalCostForTaskAsync_SumsMultipleEntries_ForTheSameTask()
    {
        var store = new FakeCostLogEntryStore();
        var logger = CreateLogger(store);

        await logger.LogAsync(MakeEntry(1L, inputTokens: 1_000_000, outputTokens: 0));
        await logger.LogAsync(MakeEntry(1L, inputTokens: 1_000_000, outputTokens: 0));

        var total = await logger.GetTotalCostForTaskAsync(1L);

        Assert.Equal(4.00m, total); // 2x ($2/MTok de input)
    }

    [Fact]
    public async Task GetTotalCostForTaskAsync_DoesNotIncludeEntries_FromOtherTasks()
    {
        var store = new FakeCostLogEntryStore();
        var logger = CreateLogger(store);

        await logger.LogAsync(MakeEntry(1L, inputTokens: 1_000_000, outputTokens: 0));
        await logger.LogAsync(MakeEntry(2L, inputTokens: 1_000_000, outputTokens: 0));

        var totalTask1 = await logger.GetTotalCostForTaskAsync(1L);

        Assert.Equal(2.00m, totalTask1);
    }

    [Fact]
    public async Task GetTotalCostSinceAsync_ExcludesEntries_BeforeTheCutoff()
    {
        var store = new FakeCostLogEntryStore();
        var logger = CreateLogger(store);
        await logger.LogAsync(MakeEntry(1L, inputTokens: 1_000_000, outputTokens: 0));

        // O corte é no futuro em relação ao registro que acabou de ser gravado (CreatedUtc = agora).
        var future = DateTimeOffset.UtcNow.AddMinutes(5);
        var total = await logger.GetTotalCostSinceAsync("acquisition_agent", future);

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task GetTotalCostSinceAsync_IsolatesByAgentName()
    {
        var store = new FakeCostLogEntryStore();
        var logger = CreateLogger(store);
        await logger.LogAsync(MakeEntry(1L, agentName: "acquisition_agent", inputTokens: 1_000_000, outputTokens: 0));
        await logger.LogAsync(MakeEntry(2L, agentName: "ads_agent", inputTokens: 1_000_000, outputTokens: 0));

        var since = DateTimeOffset.UtcNow.AddMinutes(-1);
        var totalAcquisition = await logger.GetTotalCostSinceAsync("acquisition_agent", since);

        Assert.Equal(2.00m, totalAcquisition);
    }

    [Fact]
    public async Task LogAsync_Throws_WhenModelHasNoRegisteredRate()
    {
        var store = new FakeCostLogEntryStore();
        var logger = CreateLogger(store); // catálogo só conhece "test-model"

        await Assert.ThrowsAsync<InvalidOperationException>(() => logger.LogAsync(
            MakeEntry(1L, inputTokens: 100, outputTokens: 100, model: "modelo-desconhecido")));
    }

    private static ModelCallCost MakeEntry(
        long taskId,
        int inputTokens,
        int outputTokens,
        string agentName = "acquisition_agent",
        string model = "test-model") => new ModelCallCost(taskId, agentName, agentName, model, ActivityComplexity.Planning, inputTokens, outputTokens, DateTimeOffset.UtcNow)
    {
    };
}
