using AutonomiaSaaS.Modules.BusinessCore.Domain;
using Xunit;

namespace AutonomiaSaaS.Modules.BusinessCore.Tests;

public class AgentTaskStateMachineTests
{
    // --- Caminho de alto risco: proposta → aguardando_aprovacao → aprovada → executando → concluída

    [Fact]
    public void Validate_CaminhoAltoRisco_PermiteTodaSequencia()
    {
        AgentTaskStateMachine.Validate(AgentTaskStatus.Proposta, AgentTaskStatus.AguardandoAprovacao);
        AgentTaskStateMachine.Validate(AgentTaskStatus.AguardandoAprovacao, AgentTaskStatus.Aprovada);
        AgentTaskStateMachine.Validate(AgentTaskStatus.Aprovada, AgentTaskStatus.Executando);
        AgentTaskStateMachine.Validate(AgentTaskStatus.Executando, AgentTaskStatus.Concluida);
        // Nenhuma exceção lançada = sucesso.
    }

    // --- Caminho de baixo risco: proposta → auto_executando → concluída

    [Fact]
    public void Validate_CaminhoBaixoRisco_PermiteAutoExecucao()
    {
        AgentTaskStateMachine.Validate(AgentTaskStatus.Proposta, AgentTaskStatus.AutoExecutando);
        AgentTaskStateMachine.Validate(AgentTaskStatus.AutoExecutando, AgentTaskStatus.Concluida);
    }

    // --- Rejeição e expiração

    [Fact]
    public void Validate_AguardandoAprovacao_PermiteRejeitar()
    {
        AgentTaskStateMachine.Validate(AgentTaskStatus.AguardandoAprovacao, AgentTaskStatus.Rejeitada);
    }

    [Fact]
    public void Validate_AguardandoAprovacao_PermiteExpirarPorTimeout()
    {
        AgentTaskStateMachine.Validate(AgentTaskStatus.AguardandoAprovacao, AgentTaskStatus.Expirada);
    }

    // --- Reversão (rollback_action)

    [Theory]
    [InlineData(AgentTaskStatus.AutoExecutando)]
    [InlineData(AgentTaskStatus.Executando)]
    public void Validate_ExecucaoQueTerminouOk_PermiteReverter(AgentTaskStatus estadoDeExecucao)
    {
        AgentTaskStateMachine.Validate(estadoDeExecucao, AgentTaskStatus.Revertida);
    }

    // --- Transições proibidas que mais importa nunca acontecerem por engano

    [Fact]
    public void Validate_Proposta_NaoPodePularDiretoParaExecutando()
    {
        // Isso pularia o classificador de risco inteiro — é o bug mais perigoso
        // possível nesta máquina de estados, por isso tem teste dedicado.
        Assert.Throws<InvalidStateTransitionException>(() =>
            AgentTaskStateMachine.Validate(AgentTaskStatus.Proposta, AgentTaskStatus.Executando));
    }

    [Fact]
    public void Validate_AguardandoAprovacao_NaoPodePularDiretoParaExecutando()
    {
        // Isso pularia o painel de aprovação humana (seção 4 da arquitetura).
        Assert.Throws<InvalidStateTransitionException>(() =>
            AgentTaskStateMachine.Validate(AgentTaskStatus.AguardandoAprovacao, AgentTaskStatus.Executando));
    }

    [Fact]
    public void Validate_Rejeitada_NaoPermiteNenhumaTransicaoDeSaida()
    {
        Assert.Throws<InvalidStateTransitionException>(() =>
            AgentTaskStateMachine.Validate(AgentTaskStatus.Rejeitada, AgentTaskStatus.Executando));
    }

    [Theory]
    [InlineData(AgentTaskStatus.Concluida)]
    [InlineData(AgentTaskStatus.Rejeitada)]
    [InlineData(AgentTaskStatus.Expirada)]
    [InlineData(AgentTaskStatus.Revertida)]
    public void IsTerminal_EstadosFinais_RetornaTrueSemNenhumaTransicaoPermitida(AgentTaskStatus status)
    {
        Assert.True(AgentTaskStateMachine.IsTerminal(status));
        Assert.Empty(AgentTaskStateMachine.GetAllowedNextStates(status));
    }

    [Theory]
    [InlineData(AgentTaskStatus.Proposta)]
    [InlineData(AgentTaskStatus.AguardandoAprovacao)]
    [InlineData(AgentTaskStatus.Aprovada)]
    [InlineData(AgentTaskStatus.AutoExecutando)]
    [InlineData(AgentTaskStatus.Executando)]
    public void IsTerminal_EstadosIntermediarios_RetornaFalse(AgentTaskStatus status)
    {
        Assert.False(AgentTaskStateMachine.IsTerminal(status));
    }

    [Fact]
    public void CanTransition_NaoLancaExcecao_ApenasRetornaBool()
    {
        // CanTransition é a versão "segura" usada por UI (ex: desabilitar botão),
        // Validate é a versão que efetivamente protege a escrita no banco.
        Assert.False(AgentTaskStateMachine.CanTransition(
            AgentTaskStatus.Proposta, AgentTaskStatus.Concluida));
        Assert.True(AgentTaskStateMachine.CanTransition(
            AgentTaskStatus.Proposta, AgentTaskStatus.AutoExecutando));
    }

    [Fact]
    public void GetAllowedNextStates_Proposta_RetornaAsDuasRamificacoesDeRisco()
    {
        var proximosEstados = AgentTaskStateMachine.GetAllowedNextStates(AgentTaskStatus.Proposta);

        Assert.Equal(2, proximosEstados.Count);
        Assert.Contains(AgentTaskStatus.AguardandoAprovacao, proximosEstados);
        Assert.Contains(AgentTaskStatus.AutoExecutando, proximosEstados);
    }
}
