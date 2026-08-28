using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

public class ActionRiskCatalogTests
{
    [Theory]
    [InlineData(ActionRiskCatalog.LowRiskActions.GerarRascunhoPost)]
    [InlineData(ActionRiskCatalog.LowRiskActions.GerarVariacaoLandingPage)]
    [InlineData(ActionRiskCatalog.LowRiskActions.GerarRelatorio)]
    [InlineData(ActionRiskCatalog.LowRiskActions.PausarAnuncioComCpaRuim)]
    public void TryGetFixedRiskLevel_AcoesDeBaixoRiscoDaSecao4_RetornaBaixo(string actionKey)
    {
        var level = ActionRiskCatalog.TryGetFixedRiskLevel(actionKey);

        Assert.Equal(RiskLevel.Baixo, level);
    }

    [Theory]
    [InlineData(ActionRiskCatalog.HighRiskActions.PublicarAnuncioNovo)]
    [InlineData(ActionRiskCatalog.HighRiskActions.AumentarOrcamentoAnuncio)]
    [InlineData(ActionRiskCatalog.HighRiskActions.PrimeiroDisparoEmailMassa)]
    [InlineData(ActionRiskCatalog.HighRiskActions.AcaoPagamentoOuReembolso)]
    [InlineData(ActionRiskCatalog.HighRiskActions.DeployProducao)]
    public void TryGetFixedRiskLevel_AcoesDeAltoRiscoDaSecao4_RetornaAlto(string actionKey)
    {
        var level = ActionRiskCatalog.TryGetFixedRiskLevel(actionKey);

        Assert.Equal(RiskLevel.Alto, level);
    }

    [Fact]
    public void TryGetFixedRiskLevel_AcaoNaoCatalogada_RetornaNull()
    {
        var level = ActionRiskCatalog.TryGetFixedRiskLevel("acao_que_nao_existe_no_catalogo");

        Assert.Null(level);
    }

    [Fact]
    public void TryGetFixedRiskLevel_ChaveComCasingDiferente_AindaEncontra()
    {
        // O catálogo usa StringComparer.OrdinalIgnoreCase de propósito —
        // um agente passando "Deploy_Producao" por engano não deveria
        // silenciosamente cair no fallback de "ação desconhecida".
        var level = ActionRiskCatalog.TryGetFixedRiskLevel("DEPLOY_PRODUCAO");

        Assert.Equal(RiskLevel.Alto, level);
    }

    [Fact]
    public void IsKnownAction_AcaoPagamento_RetornaTrue()
    {
        Assert.True(ActionRiskCatalog.IsKnownAction(ActionRiskCatalog.HighRiskActions.AcaoPagamentoOuReembolso));
    }

    [Fact]
    public void IsKnownAction_AcaoDesconhecida_RetornaFalse()
    {
        Assert.False(ActionRiskCatalog.IsKnownAction("gasto_ads_generico"));
    }
}
