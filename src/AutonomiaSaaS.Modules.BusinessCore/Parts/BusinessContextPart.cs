using OrchardCore.ContentManagement;

namespace AutonomiaSaaS.Modules.BusinessCore.Parts;

/// <summary>
/// Um item de BusinessContext por tenant (seção 6 do documento de arquitetura).
/// Como cada tenant do Orchard Core já é um cliente isolado do SaaS, não
/// existe campo de business_id redundante aqui — a separação de dados entre
/// clientes é feita pelo próprio isolamento de tenant, não por uma coluna.
/// </summary>
public sealed class BusinessContextPart : ContentPart
{
    /// <summary>
    /// Metas do negócio em texto livre, uma por linha. Fase 1 não estrutura
    /// isso além de texto — estruturar metas com prazo/métrica é trabalho do
    /// Agente de Planejamento (seção 2 do documento de arquitetura), que
    /// ainda não existe como módulo de código.
    /// </summary>
    public string Goals { get; set; } = string.Empty;

    /// <summary>
    /// JSON serializado de tetos de gasto, ex: {"ads_diario": 50, "ads_mensal": 1000}.
    /// Guardado como texto (não como campos estruturados) porque a Fase 1 não usa
    /// isso ainda (sem Agente de Ads) — o schema já nasce compatível com o formato
    /// da seção 6 da arquitetura, sem forçar uma migração de Content Type quando
    /// o Agente de Ads chegar na Fase 3.
    /// </summary>
    public string BudgetCapsJson { get; set; } = "{}";

    public string BrandVoice { get; set; } = string.Empty;

    /// <summary>
    /// Nomes de integrações habilitadas para este tenant, ex: ["vercel", "github"].
    /// Controla quais Agentes/atividades de workflow este cliente pode acionar —
    /// não é uma lista de credenciais (essas ficam no Key Vault, nunca aqui).
    /// </summary>
    public IList<string> IntegrationsEnabled { get; set; } = new List<string>();
}
