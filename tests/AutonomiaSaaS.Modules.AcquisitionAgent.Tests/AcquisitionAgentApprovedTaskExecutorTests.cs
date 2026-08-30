using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;
using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using Xunit;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Tests;

public class AcquisitionAgentApprovedTaskExecutorTests
{
    private sealed class FakeOrchestrator : IAcquisitionAgentOrchestrator
    {
        public string? LastProductionTaskId { get; private set; }
        public string? LastStagingDeploymentId { get; private set; }

        public Task<GenerateLandingPageResult> GenerateLandingPageAsync(
            string tenantId, string businessDescription, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Não usado neste teste.");

        public Task<PromoteToProductionResult> PromoteToProductionAsync(
            string productionTaskId, string stagingDeploymentId, CancellationToken cancellationToken = default)
        {
            LastProductionTaskId = productionTaskId;
            LastStagingDeploymentId = stagingDeploymentId;
            return Task.FromResult(new PromoteToProductionResult(AgentTaskStatus.Concluida, "https://producao.example.com"));
        }
    }

    [Fact]
    public async Task ExecuteApprovedTaskAsync_PayloadComCamelCase_ExtraiStagingDeploymentIdCorretamente()
    {
        // Este é exatamente o formato que AcquisitionAgentOrchestrator grava
        // de verdade (JsonSerializer.Serialize de um objeto anônimo em
        // camelCase) — o teste existe para travar o bug real de
        // case-sensitivity que corrigi ao escrever este arquivo.
        var orchestrator = new FakeOrchestrator();
        var executor = new AcquisitionAgentApprovedTaskExecutor(orchestrator);
        var task = new AgentTaskPart
        {
            AgentName = "acquisition_agent",
            PayloadJson = """{"stagingDeploymentId": "staging_dep_42"}"""
        };

        await executor.ExecuteApprovedTaskAsync(task, "task_producao_1");

        Assert.Equal("task_producao_1", orchestrator.LastProductionTaskId);
        Assert.Equal("staging_dep_42", orchestrator.LastStagingDeploymentId);
    }

    [Fact]
    public async Task ExecuteApprovedTaskAsync_PayloadSemStagingDeploymentId_LancaExcecaoClara()
    {
        var orchestrator = new FakeOrchestrator();
        var executor = new AcquisitionAgentApprovedTaskExecutor(orchestrator);
        var task = new AgentTaskPart { AgentName = "acquisition_agent", PayloadJson = "{}" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteApprovedTaskAsync(task, "task_producao_1"));

        Assert.Contains("stagingDeploymentId", exception.Message);
    }

    [Fact]
    public void AgentName_RetornaAcquisitionAgent()
    {
        var executor = new AcquisitionAgentApprovedTaskExecutor(new FakeOrchestrator());

        Assert.Equal("acquisition_agent", executor.AgentName);
    }
}
