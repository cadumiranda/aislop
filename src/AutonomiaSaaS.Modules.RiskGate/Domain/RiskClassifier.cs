using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Services;

namespace AutonomiaSaaS.Modules.RiskGate.Domain;

public sealed class RiskClassifier : IRiskClassifier
{
    private readonly IBusinessContextStore _businessContextStore;

    public RiskClassifier(IBusinessContextStore businessContextStore)
    {
        _businessContextStore = businessContextStore;
    }

    public async Task<RiskClassificationResult> ClassifyAsync(
        RiskClassificationRequest request, CancellationToken cancellationToken = default)
    {
        // Dimensão 1: tipo de ação, regra fixa. Sempre tem prioridade sobre
        // magnitude — ver seção 4 da arquitetura.
        var fixedLevel = ActionRiskCatalog.TryGetFixedRiskLevel(request.ActionKey);
        if (fixedLevel is not null)
        {
            return new RiskClassificationResult(
                RiskLevel: fixedLevel.Value,
                Reason: $"Regra fixa para a ação '{request.ActionKey}'.");
        }

        // Dimensão 2: magnitude, dinâmica por tenant. Só chega aqui se a ação
        // não está no catálogo fixo.
        if (request.SpendAmount is not null && request.SpendCapKey is not null)
        {
            var businessContext = await _businessContextStore
                .GetAsync(cancellationToken)
                .ConfigureAwait(false);

            if (businessContext is null)
            {
                // Sem contexto de negócio configurado para o tenant — mesmo
                // princípio de fail closed do BudgetCapEvaluator: melhor
                // pedir aprovação do que assumir um teto que não existe.
                return new RiskClassificationResult(
                    RiskLevel: RiskLevel.Alto,
                    Reason: "BusinessContext do tenant não encontrado; classificado como alto risco por segurança.");
            }

            var level = BudgetCapEvaluator.Evaluate(
                businessContext.BudgetCapsJson, request.SpendCapKey, request.SpendAmount.Value);

            var cap = BudgetCapEvaluator.TryGetCap(businessContext.BudgetCapsJson, request.SpendCapKey);
            var reason = cap is null
                ? $"Teto '{request.SpendCapKey}' não configurado para este tenant; classificado como alto risco por segurança."
                : $"Gasto estimado {request.SpendAmount:C} comparado ao teto '{request.SpendCapKey}' de {cap:C}.";

            return new RiskClassificationResult(level, reason);
        }

        // Ação desconhecida e sem informação de gasto para avaliar magnitude —
        // mesmo princípio de segurança: classifica como alto risco em vez de
        // assumir baixo risco por omissão. Uma ação nova de um agente futuro
        // deveria ser adicionada ao ActionRiskCatalog antes de ir para
        // produção, não depender deste caminho de fallback continuamente.
        return new RiskClassificationResult(
            RiskLevel: RiskLevel.Alto,
            Reason: $"Ação '{request.ActionKey}' não catalogada e sem dado de gasto para avaliar magnitude; " +
                    "classificada como alto risco por segurança.");
    }
}
