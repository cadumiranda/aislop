using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using AutonomiaSaaS.Modules.RiskGate.Services;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

public class TaskRiskGatewayTests
{
    private static ProposeActionRequest BuildRequest(
        decimal? spendAmount = null, string? spendCapKey = null, TimeSpan? approvalTimeout = null) => new(
        AgentName: "acquisition_agent",
        ActionKey: "gerar_variacao_landing_page",
        ActionDescription: "Gerar landing page para cafeteria",
        EstimatedCostTokens: 500,
        RollbackAction: "redeploy_commit_anterior",
        SpendAmount: spendAmount,
        SpendCapKey: spendCapKey,
        ApprovalTimeout: approvalTimeout);

    [Fact]
    public async Task ProposeActionAsync_ClassificacaoBaixoRisco_TerminaEmAutoExecutando()
    {
        var store = new FakeAgentTaskStore();
        var classifier = new FakeRiskClassifier(new RiskClassificationResult(RiskLevel.Baixo, "teste"));
        var gateway = new TaskRiskGateway(store, classifier, TimeProvider.System);

        var result = await gateway.ProposeActionAsync(BuildRequest());

        Assert.Equal(AgentTaskStatus.AutoExecutando, result.Status);
        Assert.Equal(RiskLevel.Baixo, result.RiskLevel);

        var persisted = store.AllTasks[result.TaskId];
        Assert.Equal(AgentTaskStatus.AutoExecutando, persisted.Status);
        Assert.Null(persisted.ApprovalTimeout); // baixo risco nunca deveria ter timeout de aprovação
    }

    [Fact]
    public async Task ProposeActionAsync_ClassificacaoAltoRisco_TerminaEmAguardandoAprovacaoComTimeout()
    {
        var store = new FakeAgentTaskStore();
        var classifier = new FakeRiskClassifier(new RiskClassificationResult(RiskLevel.Alto, "teste"));
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-08-28T12:00:00Z"));
        var gateway = new TaskRiskGateway(store, classifier, timeProvider);

        var result = await gateway.ProposeActionAsync(BuildRequest(approvalTimeout: TimeSpan.FromHours(48)));

        Assert.Equal(AgentTaskStatus.AguardandoAprovacao, result.Status);

        var persisted = store.AllTasks[result.TaskId];
        Assert.Equal(AgentTaskStatus.AguardandoAprovacao, persisted.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-08-30T12:00:00Z"), persisted.ApprovalTimeout);
    }

    [Fact]
    public async Task ProposeActionAsync_SemTimeoutExplicito_UsaPadraoDe48Horas()
    {
        var store = new FakeAgentTaskStore();
        var classifier = new FakeRiskClassifier(new RiskClassificationResult(RiskLevel.Alto, "teste"));
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-08-28T00:00:00Z"));
        var gateway = new TaskRiskGateway(store, classifier, timeProvider);

        var result = await gateway.ProposeActionAsync(BuildRequest());

        var persisted = store.AllTasks[result.TaskId];
        Assert.Equal(DateTimeOffset.Parse("2026-08-30T00:00:00Z"), persisted.ApprovalTimeout); // +48h
    }

    [Fact]
    public async Task ProposeActionAsync_NuncaDeixaTarefaParadaEmProposta()
    {
        // Regra implícita mais importante do gateway: toda tarefa proposta
        // por ele já sai do estado inicial Proposta, seja para
        // AutoExecutando ou AguardandoAprovacao. Nunca deveria existir uma
        // tarefa "esquecida" em Proposta depois de passar por este gateway.
        var store = new FakeAgentTaskStore();
        var classifierBaixo = new FakeRiskClassifier(new RiskClassificationResult(RiskLevel.Baixo, "teste"));
        var classifierAlto = new FakeRiskClassifier(new RiskClassificationResult(RiskLevel.Alto, "teste"));

        var gatewayBaixo = new TaskRiskGateway(store, classifierBaixo, TimeProvider.System);
        var gatewayAlto = new TaskRiskGateway(store, classifierAlto, TimeProvider.System);

        await gatewayBaixo.ProposeActionAsync(BuildRequest());
        await gatewayAlto.ProposeActionAsync(BuildRequest());

        Assert.All(store.AllTasks.Values, task => Assert.NotEqual(AgentTaskStatus.Proposta, task.Status));
    }

    /// <summary>
    /// TimeProvider fake mínimo — evita adicionar a dependência de pacote
    /// Microsoft.Extensions.TimeProvider.Testing só para um cenário simples
    /// de "hora fixa configurável".
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public ManualTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
