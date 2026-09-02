using AutonomiaSaaS.Modules.BusinessCore.Indexes;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace AutonomiaSaaS.Modules.BusinessCore;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("BusinessContextPart", part => part
            .Attachable(false) // parte de um único Content Type, não anexável livremente
            .WithDescription("Contexto do negócio de um tenant: metas, tetos de gasto, tom de marca."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync("BusinessContext", type => type
            .WithPart("BusinessContextPart")
            .Creatable(false)   // criado uma vez por tenant via processo de onboarding, não pelo editor de conteúdo
            .Listable(false)
            .Draftable(false)); // não faz sentido ter rascunho de contexto de negócio

        await _contentDefinitionManager.AlterPartDefinitionAsync("AgentTaskPart", part => part
            .Attachable(false)
            .WithDescription("Uma tarefa proposta por um agente, com risco, status e plano de rollback."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync("AgentTask", type => type
            .WithPart("AgentTaskPart")
            .Creatable(false)   // tarefas são criadas via AgentTaskStore, não pelo editor de conteúdo
            .Listable(true)     // aparece no painel de aprovação
            .Draftable(false)); // uma AgentTask não tem estado de rascunho, só os estados da seção 6

        SchemaBuilder.CreateMapIndexTableAsync(typeof(AgentTaskPartIndex), table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("AgentName", column => column.WithLength(64))
            .Column<int>("RiskLevel")
            .Column<int>("Status")
            .Column<DateTimeOffset>("ApprovalTimeout", column => column.Nullable()), null);

        SchemaBuilder.AlterIndexTableAsync(typeof(AgentTaskPartIndex), table => table
            .CreateIndex("IDX_AgentTaskPartIndex_Status", "Status", "ApprovalTimeout"), null);

        return 1;
    }
}
