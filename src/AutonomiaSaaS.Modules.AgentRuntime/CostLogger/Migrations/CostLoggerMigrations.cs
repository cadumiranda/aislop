using AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Storage;
using OrchardCore.Data.Migration.Records;
using YesSql.Sql;

namespace AutonomiaSaaS.Modules.AgentRuntime.CostLogger.Migrations;

public sealed class CostLoggerMigrations : DataMigration
{
    private readonly ISchemaBuilder _schemaBuilder;

    public CostLoggerMigrations(ISchemaBuilder schemaBuilder)
    {
        _schemaBuilder = schemaBuilder;
    }

    public int Create()
    {
        _schemaBuilder.CreateMapIndexTable<CostLogEntryIndex>(table => table
            .Column<string>("TaskId", column => column.WithLength(200))
            .Column<string>("AgentName", column => column.WithLength(200))
            .Column<DateTime>("CreatedUtc"));

        _schemaBuilder.AlterIndexTable<CostLogEntryIndex>(table => table
            .CreateIndex("IDX_CostLogEntryIndex_TaskId", "TaskId"));

        _schemaBuilder.AlterIndexTable<CostLogEntryIndex>(table => table
            .CreateIndex("IDX_CostLogEntryIndex_AgentName_CreatedUtc", "AgentName", "CreatedUtc"));

        return 1;
    }
}
