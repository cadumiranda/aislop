using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;
using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using Xunit;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Tests;

public class AcquisitionAgentOrchestratorTests
{
    private const string FakeModelResponse =
        "Sua cafeteria, do jeito que você sempre sonhou\n---\n<html><body>Landing page</body></html>";

    private static (AcquisitionAgentOrchestrator orchestrator, FakeAgentRuntime runtime,
        FakeTaskRiskGateway gateway, FakeAgentTaskStore store, FakeDeploymentClient deployment) BuildOrchestrator()
    {
        var runtime = new FakeAgentRuntime(FakeModelResponse);
        var store = new FakeAgentTaskStore();
        var gateway = new FakeTaskRiskGateway(store);
        var deployment = new FakeDeploymentClient();
        var orchestrator = new AcquisitionAgentOrchestrator(runtime, gateway, store, deployment);
        return (orchestrator, runtime, gateway, store, deployment);
    }

    // --- GenerateLandingPageAsync: caminho feliz completo ---

    [Fact]
    public async Task GenerateLandingPageAsync_StagingSaudavel_ConcluiTarefaDeBaixoRiscoEProdePromocao()
    {
        var (orchestrator, runtime, gateway, store, _) = BuildOrchestrator();

        var result = await orchestrator.GenerateLandingPageAsync("tenant_a", "Uma cafeteria em Botafogo");

        // O modelo foi chamado com a descrição do negócio, complexidade Planning.
        Assert.Equal("Uma cafeteria em Botafogo", runtime.LastRequest!.UserMessage);
        Assert.Equal(AgentRuntime.ModelRouting.ActivityComplexity.Planning, runtime.LastRequest.Complexity);

        // Duas propostas de ação: uma de baixo risco (geração), uma de alto risco (deploy produção).
        Assert.Equal(2, gateway.Calls.Count);
        Assert.Equal(ActionRiskCatalog.LowRiskActions.GerarVariacaoLandingPage, gateway.Calls[0].ActionKey);
        Assert.Equal(ActionRiskCatalog.HighRiskActions.DeployProducao, gateway.Calls[1].ActionKey);

        // A tarefa de baixo risco termina Concluida (staging saudável).
        Assert.Equal(AgentTaskStatus.Concluida, result.LandingPageTaskStatus);
        Assert.Equal(AgentTaskStatus.Concluida, store.AllTasks[result.LandingPageTaskId].Status);

        // A tarefa de produção existe e ficou aguardando aprovação humana.
        Assert.NotNull(result.ProductionApprovalTaskId);
        Assert.Equal("https://staging.example.com", result.StagingUrl);
    }

    [Fact]
    public async Task GenerateLandingPageAsync_StagingSaudavel_NuncaPromoveParaProducaoSemAprovacao()
    {
        // Verificação explícita do princípio da seção 4/5 da arquitetura:
        // deploy em produção é SEMPRE alto risco, mesmo quando tudo deu certo
        // em staging. GenerateLandingPageAsync nunca deveria, sozinho, deixar
        // uma tarefa de produção em qualquer status diferente de
        // AguardandoAprovacao.
        var (orchestrator, _, _, store, _) = BuildOrchestrator();

        var result = await orchestrator.GenerateLandingPageAsync("tenant_a", "Uma cafeteria em Botafogo");

        var productionTask = store.AllTasks[result.ProductionApprovalTaskId!];
        Assert.Equal(AgentTaskStatus.AguardandoAprovacao, productionTask.Status);
    }

    // --- GenerateLandingPageAsync: staging não saudável ---

    [Fact]
    public async Task GenerateLandingPageAsync_StagingNaoSaudavel_ReverteTarefaENaoProdePromocao()
    {
        var (orchestrator, _, gateway, store, deployment) = BuildOrchestrator();
        deployment.StagingHealthAfterDeploy = DeploymentStatus.Unhealthy;

        var result = await orchestrator.GenerateLandingPageAsync("tenant_a", "Uma cafeteria em Botafogo");

        Assert.Equal(AgentTaskStatus.Revertida, result.LandingPageTaskStatus);
        Assert.Null(result.ProductionApprovalTaskId);
        Assert.Null(result.StagingUrl);

        // Só UMA proposta de ação foi feita — a de geração. A de deploy em
        // produção nunca deveria ser proposta se staging nem funcionou.
        Assert.Single(gateway.Calls);
        Assert.Equal(AgentTaskStatus.Revertida, store.AllTasks[result.LandingPageTaskId].Status);
    }

    // --- PromoteToProductionAsync: guarda de segurança ---

    [Fact]
    public async Task PromoteToProductionAsync_TarefaNaoAprovada_LancaExcecaoENuncaChamaDeploy()
    {
        var (orchestrator, _, _, store, deployment) = BuildOrchestrator();
        store.Seed("task_producao", AgentTaskStatus.AguardandoAprovacao); // ainda não aprovada

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.PromoteToProductionAsync("task_producao", "staging_dep_1"));

        Assert.False(deployment.RollbackCalled);
    }

    // --- PromoteToProductionAsync: caminho feliz ---

    [Fact]
    public async Task PromoteToProductionAsync_TarefaAprovadaEDeploySaudavel_ConcluiComUrlDeProducao()
    {
        var (orchestrator, _, _, store, _) = BuildOrchestrator();
        store.Seed("task_producao", AgentTaskStatus.Aprovada);

        var result = await orchestrator.PromoteToProductionAsync("task_producao", "staging_dep_1");

        Assert.Equal(AgentTaskStatus.Concluida, result.FinalStatus);
        Assert.Equal("https://producao.example.com", result.ProductionUrl);
        Assert.Equal(AgentTaskStatus.Concluida, store.AllTasks["task_producao"].Status);
    }

    // --- PromoteToProductionAsync: falha depois de aprovada (rollback_action) ---

    [Fact]
    public async Task PromoteToProductionAsync_DeployLancaExcecao_ChamaRollbackEMarcaRevertida()
    {
        var (orchestrator, _, _, store, deployment) = BuildOrchestrator();
        store.Seed("task_producao", AgentTaskStatus.Aprovada);
        deployment.ThrowOnPromote = true;

        var result = await orchestrator.PromoteToProductionAsync("task_producao", "staging_dep_1");

        Assert.Equal(AgentTaskStatus.Revertida, result.FinalStatus);
        Assert.Null(result.ProductionUrl);
        Assert.True(deployment.RollbackCalled);
        Assert.Equal("staging_dep_1", deployment.LastRollbackDeploymentId);
        Assert.Equal(AgentTaskStatus.Revertida, store.AllTasks["task_producao"].Status);
    }

    [Fact]
    public async Task PromoteToProductionAsync_DeploySobeMasFicaUnhealthy_ChamaRollbackEMarcaRevertida()
    {
        // Diferente do teste anterior: aqui a chamada não lança exceção, só
        // retorna um deployment com Status = Unhealthy. Os dois caminhos de
        // falha (exceção vs. resultado não saudável) precisam levar ao mesmo
        // desfecho de segurança.
        var (orchestrator, _, _, store, deployment) = BuildOrchestrator();
        store.Seed("task_producao", AgentTaskStatus.Aprovada);
        deployment.ProductionHealthAfterPromote = DeploymentStatus.Unhealthy;

        var result = await orchestrator.PromoteToProductionAsync("task_producao", "staging_dep_1");

        Assert.Equal(AgentTaskStatus.Revertida, result.FinalStatus);
        Assert.True(deployment.RollbackCalled);
    }

    [Fact]
    public async Task PromoteToProductionAsync_TarefaInexistente_LancaExcecao()
    {
        var (orchestrator, _, _, _, _) = BuildOrchestrator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.PromoteToProductionAsync("task_nao_existe", "staging_dep_1"));
    }
}
