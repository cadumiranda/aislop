using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace AutonomiaSaaS.Modules.BusinessCore.Indexes;

/// <summary>
/// Índice usado pelo painel de aprovação (seção 8 da especificação técnica)
/// para consultar tarefas por Status sem escanear o conteúdo inteiro —
/// principalmente a consulta "todas as tarefas com Status = AguardandoAprovacao
/// ordenadas por ApprovalTimeout mais próximo de vencer", que roda com
/// frequência (tanto no carregamento do painel quanto no IBackgroundTask
/// de expiração).
/// </summary>
public sealed class AgentTaskPartIndex : MapIndex
{
    public string ContentItemId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public AgentTaskStatus Status { get; set; }
    public DateTimeOffset? ApprovalTimeout { get; set; }
}

public sealed class AgentTaskPartIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<AgentTaskPartIndex>()
            .Map(contentItem =>
            {
                // Só indexa a versão publicada/mais recente do item — evita
                // que rascunhos (draft) apareçam em consultas do painel de
                // aprovação, que deve refletir apenas tarefas reais em curso.
                if (!contentItem.Latest)
                {
                    return null;
                }

                var part = contentItem.As<AgentTaskPart>();
                if (part is null)
                {
                    return null;
                }

                return new AgentTaskPartIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    AgentName = part.AgentName,
                    RiskLevel = part.RiskLevel,
                    Status = part.Status,
                    ApprovalTimeout = part.ApprovalTimeout
                };
            });
    }
}
