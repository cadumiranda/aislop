using AutonomiaSaaS.Modules.BusinessCore.Domain;

namespace AutonomiaSaaS.Modules.RiskGate.Domain;

/// <summary>
/// Tudo que a classificação de risco precisa saber sobre uma ação proposta.
/// SpendAmount e SpendCapKey só são relevantes quando ActionKey não está no
/// catálogo fixo (ActionRiskCatalog) — para ações já catalogadas, esses dois
/// campos são ignorados e a regra fixa sempre vence, conforme a seção 4 da
/// arquitetura ("tipo de ação" tem prioridade sobre "magnitude").
/// </summary>
public sealed record RiskClassificationRequest(
    string ActionKey,
    decimal? SpendAmount = null,
    string? SpendCapKey = null
);

public sealed record RiskClassificationResult(
    RiskLevel RiskLevel,
    string Reason
);

/// <summary>
/// Combina as duas dimensões da seção 4 da arquitetura: tipo de ação (regra
/// fixa, ActionRiskCatalog) e magnitude (dinâmica, BudgetCapEvaluator,
/// comparada ao budget_caps do tenant atual). A implementação real
/// (RiskClassifier) é quem resolve o BusinessContextPart do tenant — esta
/// interface não expõe esse detalhe para quem consome a classificação.
/// </summary>
public interface IRiskClassifier
{
    Task<RiskClassificationResult> ClassifyAsync(
        RiskClassificationRequest request, CancellationToken cancellationToken = default);
}
