using System;
using System.Threading;
using System.Threading.Tasks;
using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;
using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using AutonomiaSaaS.Modules.AgentRuntime;
using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using AutonomiaSaaS.Modules.RiskGate.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutonomiaSaaS.Tests;

public class AcquisitionAgentOrchestratorTests
{
    [Fact]
    public async Task GenerateLandingPageAsync_SuccessfulDeployment_ReturnsCompletedStagingAndProductionApprovalTask()
    {
        // Arrange
        var fakeDeploymentClient = new FakeDeploymentClient();
        var fakeAgentRuntime = new FakeAgentRuntime();
        var fakeAgentTaskStore = new FakeAgentTaskStore();
        var fakeRiskClassifier = new FakeRiskClassifier();
        var taskRiskGateway = new TaskRiskGateway(fakeAgentTaskStore, fakeRiskClassifier, TimeProvider.System);

        var orchestrator = new AcquisitionAgentOrchestrator(
            fakeAgentRuntime,
            taskRiskGateway,
            fakeAgentTaskStore,
            fakeDeploymentClient);

        // Act
        var result = await orchestrator.GenerateLandingPageAsync(
            tenantId: "tenant-123",
            businessDescription: "SaaS de automação de marketing para pequenas empresas");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AgentTaskStatus.Concluida, result.LandingPageTaskStatus);
        Assert.NotNull(result.StagingUrl);
        Assert.NotNull(result.StagingDeploymentId);
        Assert.NotNull(result.ProductionApprovalTaskId);
    }

    [Fact]
    public async Task PromoteToProductionAsync_WhenTaskApproved_PromotesSuccessfully()
    {
        // Arrange
        var fakeDeploymentClient = new FakeDeploymentClient();
        var fakeAgentRuntime = new FakeAgentRuntime();
        var fakeAgentTaskStore = new FakeAgentTaskStore();
        var fakeRiskClassifier = new FakeRiskClassifier();
        var taskRiskGateway = new TaskRiskGateway(fakeAgentTaskStore, fakeRiskClassifier, TimeProvider.System);

        var orchestrator = new AcquisitionAgentOrchestrator(
            fakeAgentRuntime,
            taskRiskGateway,
            fakeAgentTaskStore,
            fakeDeploymentClient);

        // Create approved task
        var taskId = await fakeAgentTaskStore.CreateAsync(new CreateAgentTaskRequest(
            AgentName: "acquisition_agent",
            Action: "Promover para produção",
            RiskLevel: RiskLevel.Alto,
            EstimatedCostTokens: 0,
            RollbackAction: "redeploy_commit_anterior"));

        await fakeAgentTaskStore.TransitionAsync(taskId, AgentTaskStatus.Aprovada);

        // Act
        var result = await orchestrator.PromoteToProductionAsync(taskId, "staging-dep-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AgentTaskStatus.Concluida, result.FinalStatus);
        Assert.NotNull(result.ProductionUrl);
    }

    [Fact]
    public async Task GenerateLandingPageAsync_WhenStagingCheckFails_RevertsTaskAndDoesNotProposeProduction()
    {
        // Arrange
        var fakeDeploymentClient = new FakeDeploymentClient { ShouldFailCheck = true };
        var fakeAgentRuntime = new FakeAgentRuntime();
        var fakeAgentTaskStore = new FakeAgentTaskStore();
        var fakeRiskClassifier = new FakeRiskClassifier();
        var taskRiskGateway = new TaskRiskGateway(fakeAgentTaskStore, fakeRiskClassifier, TimeProvider.System);

        var orchestrator = new AcquisitionAgentOrchestrator(
            fakeAgentRuntime,
            taskRiskGateway,
            fakeAgentTaskStore,
            fakeDeploymentClient);

        // Act
        var result = await orchestrator.GenerateLandingPageAsync(
            tenantId: "tenant-123",
            businessDescription: "SaaS de teste com falha de deploy");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AgentTaskStatus.Revertida, result.LandingPageTaskStatus);
        Assert.Null(result.StagingUrl);
        Assert.Null(result.ProductionApprovalTaskId);
    }

    [Fact]
    public async Task PromoteToProductionAsync_WhenProductionFails_TriggersRollbackAndRevertsTask()
    {
        // Arrange
        var fakeDeploymentClient = new FakeDeploymentClient { ShouldFailProduction = true };
        var fakeAgentRuntime = new FakeAgentRuntime();
        var fakeAgentTaskStore = new FakeAgentTaskStore();
        var fakeRiskClassifier = new FakeRiskClassifier();
        var taskRiskGateway = new TaskRiskGateway(fakeAgentTaskStore, fakeRiskClassifier, TimeProvider.System);

        var orchestrator = new AcquisitionAgentOrchestrator(
            fakeAgentRuntime,
            taskRiskGateway,
            fakeAgentTaskStore,
            fakeDeploymentClient);

        var taskId = await fakeAgentTaskStore.CreateAsync(new CreateAgentTaskRequest(
            AgentName: "acquisition_agent",
            Action: "Promover para produção",
            RiskLevel: RiskLevel.Alto,
            EstimatedCostTokens: 0,
            RollbackAction: "redeploy_commit_anterior"));

        await fakeAgentTaskStore.TransitionAsync(taskId, AgentTaskStatus.Aprovada);

        // Act
        var result = await orchestrator.PromoteToProductionAsync(taskId, "staging-dep-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AgentTaskStatus.Revertida, result.FinalStatus);
        Assert.Null(result.ProductionUrl);
    }

    [Fact]
    public void AcquisitionAgentStartup_ResolvesFakeDeploymentClient_ByDefault()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var startup = new AutonomiaSaaS.Modules.AcquisitionAgent.Startup(config);

        // Act
        startup.ConfigureServices(services);
        var provider = services.BuildServiceProvider();
        var deploymentClient = provider.GetService<IDeploymentClient>();

        // Assert
        Assert.NotNull(deploymentClient);
        Assert.IsType<FakeDeploymentClient>(deploymentClient);
    }

    private class FakeAgentRuntime : IAgentRuntime
    {
        public Task<AgentRuntimeResult> ExecuteAsync(AgentRuntimeRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentRuntimeResult(
                Text: "Melhor SaaS de Automação\n---\n<html><body><h1>Landing Page</h1></body></html>",
                ModelUsed: "claude-3-5-sonnet",
                InputTokens: 100,
                OutputTokens: 200));
        }
    }

    private class FakeRiskClassifier : IRiskClassifier
    {
        public Task<RiskClassificationResult> ClassifyAsync(RiskClassificationRequest request, CancellationToken cancellationToken = default)
        {
            var isHighRisk = request.ActionKey == ActionRiskCatalog.HighRiskActions.DeployProducao;
            return Task.FromResult(new RiskClassificationResult(
                RiskLevel: isHighRisk ? RiskLevel.Alto : RiskLevel.Baixo,
                Reason: "Classificação fake para teste"));
        }
    }

    private class FakeAgentTaskStore : IAgentTaskStore
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AgentTaskPart> _tasks = new();
        private int _counter = 0;

        public Task<string> CreateAsync(CreateAgentTaskRequest request, CancellationToken cancellationToken = default)
        {
            var id = $"task-{Interlocked.Increment(ref _counter)}";
            var part = new AgentTaskPart
            {
                AgentName = request.AgentName,
                Action = request.Action,
                Status = AgentTaskStatus.Proposta,
                RiskLevel = request.RiskLevel,
                EstimatedCostTokens = request.EstimatedCostTokens,
                RollbackAction = request.RollbackAction
            };
            part.ContentItem = new OrchardCore.ContentManagement.ContentItem { ContentItemId = id };
            _tasks[id] = part;
            return Task.FromResult(id);
        }

        public Task<AgentTaskPart?> GetByIdAsync(string taskId, CancellationToken cancellationToken = default)
        {
            _tasks.TryGetValue(taskId, out var task);
            return Task.FromResult(task);
        }

        public Task TransitionAsync(string taskId, AgentTaskStatus newStatus, CancellationToken cancellationToken = default)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Status = newStatus;
            }
            return Task.CompletedTask;
        }

        public Task SetApprovalTimeoutAsync(string taskId, DateTimeOffset timeout, CancellationToken cancellationToken = default)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.ApprovalTimeout = timeout;
            }
            return Task.CompletedTask;
        }

        public Task<System.Collections.Generic.IReadOnlyList<AgentTaskPart>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
        {
            System.Collections.Generic.List<AgentTaskPart> list = new();
            foreach (var t in _tasks.Values)
            {
                if (t.Status == AgentTaskStatus.AguardandoAprovacao) list.Add(t);
            }
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<AgentTaskPart>>(list);
        }

        public Task<System.Collections.Generic.IReadOnlyList<AgentTaskPart>> GetExpiredApprovalsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            System.Collections.Generic.List<AgentTaskPart> list = new();
            foreach (var t in _tasks.Values)
            {
                if (t.Status == AgentTaskStatus.AguardandoAprovacao && t.ApprovalTimeout.HasValue && t.ApprovalTimeout.Value <= now)
                    list.Add(t);
            }
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<AgentTaskPart>>(list);
        }
    }
}
