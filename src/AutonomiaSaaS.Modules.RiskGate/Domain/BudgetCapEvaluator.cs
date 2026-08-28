using System.Text.Json;
using AutonomiaSaaS.Modules.BusinessCore.Domain;

namespace AutonomiaSaaS.Modules.RiskGate.Domain;

/// <summary>
/// Avalia a dimensão de MAGNITUDE da classificação de risco (seção 4 da
/// arquitetura): compara um gasto estimado contra o budget_caps do tenant,
/// armazenado como JSON em BusinessContextPart.BudgetCapsJson (seção 6).
///
/// Classe estática e pura — recebe o JSON já como string, não busca nada
/// sozinha. Quem resolve o BusinessContextPart (RiskClassifier, camada acima)
/// é responsável por buscar o contexto do tenant; esta classe só sabe
/// interpretar o formato do JSON e comparar números.
/// </summary>
public static class BudgetCapEvaluator
{
    /// <summary>
    /// Chaves conhecidas dentro de BudgetCapsJson, ex: {"ads_diario": 50, "ads_mensal": 1000}.
    /// Mantidas como constantes para não espalhar strings mágicas entre este
    /// avaliador e quem grava o BudgetCapsJson no onboarding do tenant.
    /// </summary>
    public static class CapKeys
    {
        public const string AdsDiario = "ads_diario";
        public const string AdsMensal = "ads_mensal";
    }

    /// <summary>
    /// Retorna Alto se o gasto estimado excede o teto configurado para a
    /// chave informada, Baixo caso contrário. Se a chave de teto não existir
    /// no JSON do tenant, o comportamento é "fail closed": trata como se não
    /// houvesse teto definido e classifica como Alto — silenciosamente
    /// deixar passar um gasto sem teto configurado seria o pior cenário
    /// possível para este método, então o padrão seguro é pedir aprovação.
    /// </summary>
    public static RiskLevel Evaluate(string budgetCapsJson, string capKey, decimal estimatedAmount)
    {
        var cap = TryGetCap(budgetCapsJson, capKey);

        if (cap is null)
        {
            return RiskLevel.Alto; // fail closed: sem teto configurado, exige aprovação
        }

        return estimatedAmount > cap.Value ? RiskLevel.Alto : RiskLevel.Baixo;
    }

    /// <summary>
    /// Extrai o valor de um teto específico do JSON, ou null se a chave não
    /// existir ou o JSON estiver malformado. Exposto separadamente de
    /// Evaluate para permitir, por exemplo, exibir o teto configurado na UI
    /// de onboarding sem precisar de um valor de gasto para comparar.
    /// </summary>
    public static decimal? TryGetCap(string budgetCapsJson, string capKey)
    {
        if (string.IsNullOrWhiteSpace(budgetCapsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(budgetCapsJson);
            if (document.RootElement.TryGetProperty(capKey, out var value)
                && value.TryGetDecimal(out var cap))
            {
                return cap;
            }
        }
        catch (JsonException)
        {
            // JSON malformado é tratado igual a "sem teto configurado" —
            // não deixa uma falha de parsing virar exceção não tratada em
            // pleno fluxo de classificação de risco.
        }

        return null;
    }
}
