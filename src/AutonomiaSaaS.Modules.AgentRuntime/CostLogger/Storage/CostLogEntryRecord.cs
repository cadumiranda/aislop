using YesSql.Indexes;

namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;

public sealed class CostLogEntryRecord
{
    public int Id { get; set; }
    public required long TaskId { get; set; }
    public required string AgentName { get; set; }
    public required string Model { get; set; }
    public required long InputTokens { get; set; }
    public required long OutputTokens { get; set; }
    public required decimal EstimatedCostUsd { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Índice duplo: por TaskId (custo de uma tarefa) e por AgentName+CreatedUtc (custo desde uma data).</summary>
public sealed class CostLogEntryIndex : MapIndex
{
    public long TaskId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public sealed class CostLogEntryIndexProvider : IndexProvider<CostLogEntryRecord>
{
    public override void Describe(DescribeContext<CostLogEntryRecord> context)
    {
        context.For<CostLogEntryIndex>()
            .Map(record => new CostLogEntryIndex
            {
                TaskId = record.TaskId,
                AgentName = record.AgentName,
                CreatedUtc = record.CreatedUtc,
            });
    }
}
