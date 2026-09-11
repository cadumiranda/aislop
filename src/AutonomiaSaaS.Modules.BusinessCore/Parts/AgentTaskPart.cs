using AutonomiaSaaS.Modules.BusinessCore.Domain;
using OrchardCore.ContentManagement;

namespace AutonomiaSaaS.Modules.BusinessCore.Parts;

/// <summary>
/// Representa uma tarefa proposta por um agente (seção 6 do documento de
/// arquitetura). O ContentItem.ContentItemId (herdado de ContentPart) já
/// serve como TaskId — não duplicamos isso como propriedade própria.
///
/// Mudanças de Status NUNCA devem ser feitas diretamente atribuindo esta
/// propriedade fora de AgentTaskStore. A validação de transição
/// (AgentTaskStateMachine.Validate) precisa rodar antes de qualquer escrita —
/// esta classe é só o container de dados, não impõe essa regra sozinha.
/// </summary>
public sealed class AgentTaskPart : ContentPart
{
    /// <summary>
    /// Identificador do agente responsável, ex: "acquisition_agent".
    /// Texto livre por enquanto — não um enum, porque novos agentes são
    /// adicionados via roadmap (seção 11 da arquitetura) sem que este Content
    /// Type precise ser recompilado a cada fase nova.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    public long? TaskId => ContentItem?.Id;

    public string Action { get; set; } = string.Empty;

    public RiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Custo estimado em tokens (input + output) antes da execução — base do
    /// teto de custo por tarefa da seção 8 da arquitetura. Custo real
    /// (via ICostLogger do módulo AgentRuntime) é registrado à parte;
    /// este campo é só a estimativa usada para decidir se autoriza a chamada.
    /// </summary>
    public long EstimatedCostTokens { get; set; }

    /// <summary>
    /// Descrição de como reverter esta tarefa se necessário, ex:
    /// "redeploy_commit_anterior". Nunca deve ficar vazio para tarefas em
    /// AutoExecutando — reforçado por validação de aplicação, não pelo banco.
    /// </summary>
    public string RollbackAction { get; set; } = string.Empty;

    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.Proposta;

    /// <summary>
    /// Id da instância de Workflow (OrchardCore.Workflows) responsável por
    /// executar esta tarefa. Guardado como texto porque este módulo não
    /// referencia o módulo de Workflows diretamente — evita acoplamento
    /// circular entre BusinessCore e a camada de execução.
    /// </summary>
    public string? WorkflowInstanceId { get; set; }

    /// <summary>
    /// Momento em que a aprovação expira, se o Status for AguardandoAprovacao.
    /// Nulo para qualquer outro status. Consultado por um IBackgroundTask
    /// (ver especificação técnica, seção 5) que expira aprovações vencidas.
    /// </summary>
    public DateTimeOffset? ApprovalTimeout { get; set; }

    /// <summary>
    /// Bag genérico de dados extras específicos do agente que propôs a
    /// tarefa, serializado como JSON. Ex: para deploy em produção do
    /// AcquisitionAgent, guarda {"stagingDeploymentId": "..."} — dado que só
    /// o próprio agente sabe interpretar. BusinessCore nunca lê o conteúdo
    /// deste campo, só carrega — é isso que evita BusinessCore precisar
    /// conhecer o formato de payload de cada agente presente ou futuro.
    /// Como ContentPart é serializado como JSON dentro do ContentItem por
    /// padrão no Orchard Core, adicionar esta propriedade não exige nenhuma
    /// migração de coluna de banco — só campos indexados (ver
    /// AgentTaskPartIndex) precisam disso.
    /// </summary>
    public string PayloadJson { get; set; } = "{}";
}
