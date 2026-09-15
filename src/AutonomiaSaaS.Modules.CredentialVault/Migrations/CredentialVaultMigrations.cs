using AutonomiaSaaS.Modules.CredentialVault.Storage;
using OrchardCore.Data.Migration.Records;
using YesSql.Sql;

namespace AutonomiaSaaS.Modules.CredentialVault.Migrations;

/// <summary>
/// Descoberta por convenção do Orchard Core (classe DataMigration dentro do módulo) — não
/// registrada explicitamente em Startup.cs. Verifique isso contra o padrão real do projeto
/// se o BusinessCore registrar migrations de outro jeito.
/// </summary>
public sealed class CredentialVaultMigrations : DataMigration
{
    private readonly ISchemaBuilder _schemaBuilder;

    public CredentialVaultMigrations(ISchemaBuilder schemaBuilder)
    {
        _schemaBuilder = schemaBuilder;
    }

    public int Create()
    {
        _schemaBuilder.CreateMapIndexTable<AgentCredentialIndex>(table => table
            .Column<string>("AgentName", column => column.WithLength(200))
            .Column<string>("Key", column => column.WithLength(200)));

        _schemaBuilder.AlterIndexTable<AgentCredentialIndex>(table => table
            .CreateIndex("IDX_AgentCredentialIndex_AgentName_Key", "AgentName", "Key"));

        return 1;
    }
}
