using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

public class RiskClassifierTests
{
    [Fact]
    public async Task ClassifyAsync_AcaoDePagamento_RetornaAltoPorRegraFixa_MesmoSemBusinessContext()
    {
        // Regra fixa nunca deveria precisar consultar BusinessContext —
        // FakeBusinessContextStore.Empty() garante que, se o classificador
        // tentar buscar contexto para uma ação já catalogada, o teste não
        // quebra por acaso, mas o Reason abaixo confirma que a regra fixa
        // foi de fato o caminho tomado.
        var classifier = new RiskClassifier(FakeBusinessContextStore.Empty());

        var result = await classifier.ClassifyAsync(
            new RiskClassificationRequest(ActionRiskCatalog.HighRiskActions.AcaoPagamentoOuReembolso));

        Assert.Equal(RiskLevel.Alto, result.RiskLevel);
        Assert.Contains("Regra fixa", result.Reason);
    }

    [Fact]
    public async Task ClassifyAsync_AcaoDeBaixoRisco_RetornaBaixoPorRegraFixa()
    {
        var classifier = new RiskClassifier(FakeBusinessContextStore.Empty());

        var result = await classifier.ClassifyAsync(
            new RiskClassificationRequest(ActionRiskCatalog.LowRiskActions.GerarRelatorio));

        Assert.Equal(RiskLevel.Baixo, result.RiskLevel);
    }

    [Fact]
    public async Task ClassifyAsync_GastoDeAdsDentroDoTeto_RetornaBaixo()
    {
        var store = FakeBusinessContextStore.WithBudgetCapsJson("""{ "ads_diario": 50 }""");
        var classifier = new RiskClassifier(store);

        var result = await classifier.ClassifyAsync(
            new RiskClassificationRequest(
                ActionKey: "gasto_ads_diario", // não catalogado -> cai na avaliação de magnitude
                SpendAmount: 30m,
                SpendCapKey: BudgetCapEvaluator.CapKeys.AdsDiario));

        Assert.Equal(RiskLevel.Baixo, result.RiskLevel);
    }

    [Fact]
    public async Task ClassifyAsync_GastoDeAdsAcimaDoTeto_RetornaAlto()
    {
        var store = FakeBusinessContextStore.WithBudgetCapsJson("""{ "ads_diario": 50 }""");
        var classifier = new RiskClassifier(store);

        var result = await classifier.ClassifyAsync(
            new RiskClassificationRequest(
                ActionKey: "gasto_ads_diario",
                SpendAmount: 80m,
                SpendCapKey: BudgetCapEvaluator.CapKeys.AdsDiario));

        Assert.Equal(RiskLevel.Alto, result.RiskLevel);
        Assert.Contains("80", result.Reason);
    }

    [Fact]
    public async Task ClassifyAsync_SemBusinessContextConfigurado_RetornaAltoPorSeguranca()
    {
        var classifier = new RiskClassifier(FakeBusinessContextStore.Empty());

        var result = await classifier.ClassifyAsync(
            new RiskClassificationRequest(
                ActionKey: "gasto_ads_diario",
                SpendAmount: 10m,
                SpendCapKey: BudgetCapEvaluator.CapKeys.AdsDiario));

        Assert.Equal(RiskLevel.Alto, result.RiskLevel);
        Assert.Contains("BusinessContext", result.Reason);
    }

    [Fact]
    public async Task ClassifyAsync_AcaoDesconhecidaSemDadoDeGasto_RetornaAltoPorSeguranca()
    {
        // Este é o caso mais importante de "fail closed": um agente futuro
        // chamando uma ActionKey nova, ainda não catalogada, nunca deveria
        // auto-executar por omissão.
        var classifier = new RiskClassifier(FakeBusinessContextStore.Empty());

        var result = await classifier.ClassifyAsync(
            new RiskClassificationRequest(ActionKey: "acao_totalmente_nova_do_agente_de_financas"));

        Assert.Equal(RiskLevel.Alto, result.RiskLevel);
        Assert.Contains("não catalogada", result.Reason);
    }
}
