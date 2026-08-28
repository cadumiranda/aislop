using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

public class BudgetCapEvaluatorTests
{
    private const string BudgetCapsJson = """{ "ads_diario": 50, "ads_mensal": 1000 }""";

    [Fact]
    public void Evaluate_GastoDentroDoTeto_RetornaBaixo()
    {
        var level = BudgetCapEvaluator.Evaluate(BudgetCapsJson, BudgetCapEvaluator.CapKeys.AdsDiario, 30m);

        Assert.Equal(RiskLevel.Baixo, level);
    }

    [Fact]
    public void Evaluate_GastoExatoNoTeto_RetornaBaixo()
    {
        // Regra é "excede o teto", não "atinge o teto" — igual ao teto ainda
        // é permitido sem aprovação, conforme a semântica usual de "teto".
        var level = BudgetCapEvaluator.Evaluate(BudgetCapsJson, BudgetCapEvaluator.CapKeys.AdsDiario, 50m);

        Assert.Equal(RiskLevel.Baixo, level);
    }

    [Fact]
    public void Evaluate_GastoAcimaDoTeto_RetornaAlto()
    {
        // Este é literalmente o exemplo da seção 4 da arquitetura:
        // "R$50/dia pode ser alto risco para um cliente".
        var level = BudgetCapEvaluator.Evaluate(BudgetCapsJson, BudgetCapEvaluator.CapKeys.AdsDiario, 50.01m);

        Assert.Equal(RiskLevel.Alto, level);
    }

    [Fact]
    public void Evaluate_ChaveDeTetoNaoConfigurada_RetornaAltoPorSeguranca()
    {
        var level = BudgetCapEvaluator.Evaluate(BudgetCapsJson, "chave_inexistente", 1m);

        Assert.Equal(RiskLevel.Alto, level);
    }

    [Fact]
    public void Evaluate_JsonMalformado_RetornaAltoPorSegurancaSemLancarExcecao()
    {
        var level = BudgetCapEvaluator.Evaluate("{ json inválido ][", BudgetCapEvaluator.CapKeys.AdsDiario, 1m);

        Assert.Equal(RiskLevel.Alto, level);
    }

    [Fact]
    public void Evaluate_JsonVazio_RetornaAltoPorSeguranca()
    {
        var level = BudgetCapEvaluator.Evaluate(string.Empty, BudgetCapEvaluator.CapKeys.AdsDiario, 1m);

        Assert.Equal(RiskLevel.Alto, level);
    }

    [Fact]
    public void TryGetCap_ChaveExiste_RetornaValor()
    {
        var cap = BudgetCapEvaluator.TryGetCap(BudgetCapsJson, BudgetCapEvaluator.CapKeys.AdsMensal);

        Assert.Equal(1000m, cap);
    }

    [Fact]
    public void TryGetCap_ChaveNaoExiste_RetornaNull()
    {
        var cap = BudgetCapEvaluator.TryGetCap(BudgetCapsJson, "chave_inexistente");

        Assert.Null(cap);
    }
}
