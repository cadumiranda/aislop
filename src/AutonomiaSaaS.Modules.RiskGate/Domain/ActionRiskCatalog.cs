using AutonomiaSaaS.Modules.BusinessCore.Domain;

namespace AutonomiaSaaS.Modules.RiskGate.Domain;

/// <summary>
/// Catálogo de regras FIXAS de risco por tipo de ação, exatamente como
/// listado na seção 4 do documento de arquitetura. "Fixa" aqui quer dizer:
/// não depende do budget_caps do tenant, nem de nenhum outro contexto —
/// pagamento é sempre alto risco, ponto final, para qualquer cliente.
///
/// Ações que NÃO aparecem neste catálogo são candidatas a classificação por
/// magnitude (ver BudgetCapEvaluator) — normalmente gasto de mídia paga, cujo
/// risco depende de quanto o tenant definiu como teto (seção 4: "R$50/dia
/// pode ser alto risco para um cliente e irrelevante para outro").
///
/// Classe estática e pura de propósito, sem I/O — igual à AgentTaskStateMachine,
/// esta é uma das peças que mais compensa deixar 100% coberta por teste.
/// </summary>
public static class ActionRiskCatalog
{
    /// <summary>
    /// Chaves de ação de risco BAIXO, sempre auto-executam (seção 4).
    /// </summary>
    public static class LowRiskActions
    {
        public const string GerarRascunhoPost = "gerar_rascunho_post";
        public const string GerarVariacaoLandingPage = "gerar_variacao_landing_page";
        public const string GerarRelatorio = "gerar_relatorio";
        public const string PausarAnuncioComCpaRuim = "pausar_anuncio_cpa_ruim";
    }

    /// <summary>
    /// Chaves de ação de risco ALTO, sempre exigem aprovação (seção 4),
    /// independente de magnitude ou budget_caps do tenant.
    /// </summary>
    public static class HighRiskActions
    {
        public const string PublicarAnuncioNovo = "publicar_anuncio_novo";
        public const string AumentarOrcamentoAnuncio = "aumentar_orcamento_anuncio";
        public const string PrimeiroDisparoEmailMassa = "primeiro_disparo_email_massa";
        public const string AcaoPagamentoOuReembolso = "acao_pagamento_ou_reembolso";
        public const string DeployProducao = "deploy_producao";
    }

    private static readonly IReadOnlyDictionary<string, RiskLevel> FixedRules =
        new Dictionary<string, RiskLevel>(StringComparer.OrdinalIgnoreCase)
        {
            [LowRiskActions.GerarRascunhoPost] = RiskLevel.Baixo,
            [LowRiskActions.GerarVariacaoLandingPage] = RiskLevel.Baixo,
            [LowRiskActions.GerarRelatorio] = RiskLevel.Baixo,
            [LowRiskActions.PausarAnuncioComCpaRuim] = RiskLevel.Baixo,

            [HighRiskActions.PublicarAnuncioNovo] = RiskLevel.Alto,
            [HighRiskActions.AumentarOrcamentoAnuncio] = RiskLevel.Alto,
            [HighRiskActions.PrimeiroDisparoEmailMassa] = RiskLevel.Alto,
            [HighRiskActions.AcaoPagamentoOuReembolso] = RiskLevel.Alto,
            [HighRiskActions.DeployProducao] = RiskLevel.Alto,
        };

    /// <summary>
    /// Retorna o nível de risco fixo para a chave de ação, ou null se a ação
    /// não está no catálogo fixo — nesse caso, quem chamou deve recorrer à
    /// avaliação por magnitude (BudgetCapEvaluator) antes de decidir.
    /// </summary>
    public static RiskLevel? TryGetFixedRiskLevel(string actionKey)
        => FixedRules.TryGetValue(actionKey, out var level) ? level : null;

    public static bool IsKnownAction(string actionKey) => FixedRules.ContainsKey(actionKey);
}
