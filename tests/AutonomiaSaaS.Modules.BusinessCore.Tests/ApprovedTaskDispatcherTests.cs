using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using Xunit;

namespace AutonomiaSaaS.Modules.BusinessCore.Tests;

public class ApprovedTaskDispatcherTests
{
    private sealed class FakeExecutor : IApprovedTaskExecutor
    {
        public string AgentName { get; }
        public bool WasCalled { get; private set; }
        public AgentTaskPart? LastTask { get; private set; }

        public FakeExecutor(string agentName) => AgentName = agentName;

        public Task ExecuteApprovedTaskAsync(AgentTaskPart task, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastTask = task;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchAsync_AgentComExecutorRegistrado_ChamaOExecutorCerto()
    {
        var acquisitionExecutor = new FakeExecutor("acquisition_agent");
        var outroExecutor = new FakeExecutor("outro_agente");
        var dispatcher = new ApprovedTaskDispatcher(new[] { acquisitionExecutor, outroExecutor });
        var task = new AgentTaskPart { AgentName = "acquisition_agent" };

        await dispatcher.DispatchAsync(task);

        Assert.True(acquisitionExecutor.WasCalled);
        Assert.False(outroExecutor.WasCalled);
    }

    [Fact]
    public async Task DispatchAsync_AgentNameComCasingDiferente_AindaEncontraOExecutor()
    {
        var executor = new FakeExecutor("acquisition_agent");
        var dispatcher = new ApprovedTaskDispatcher(new[] { executor });
        var task = new AgentTaskPart { AgentName = "ACQUISITION_AGENT" };

        await dispatcher.DispatchAsync(task);

        Assert.True(executor.WasCalled);
    }

    [Fact]
    public async Task DispatchAsync_NenhumExecutorRegistradoParaOAgente_LancaExcecaoClara()
    {
        var dispatcher = new ApprovedTaskDispatcher(Array.Empty<IApprovedTaskExecutor>());
        var task = new AgentTaskPart { AgentName = "agente_sem_executor" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(task));

        Assert.Contains("agente_sem_executor", exception.Message);
    }
}
