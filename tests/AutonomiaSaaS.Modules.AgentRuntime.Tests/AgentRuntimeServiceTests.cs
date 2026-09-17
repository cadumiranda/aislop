using AutonomiaSaaS.Modules.AgentRuntime.CostLogger;
using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;
using AutonomiaSaaS.Modules.AgentRuntime.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

public class AgentRuntimeServiceTests
{
    private static ModelRouter CreateRouter() => new(Options.Create(new AgentRuntimeOptions
    {
        PlanningModel = "claude-sonnet-5",
        RepetitiveModel = "claude-haiku-4-5-20251001"
    }));

    [Fact]
    public async Task ExecuteAsync_ComplexidadePlanning_UsaModeloDePlanejamentoNaChamada()
    {
        var fakeResponse = new AnthropicMessagesResponse(
            Id: "msg_1",
            Model: "claude-sonnet-5",
            Content: new[] { new AnthropicContentBlock("text", "landing page gerada") },
            Usage: new AnthropicUsage(200, 80),
            StopReason: "end_turn");
        var fakeClient = new FakeAnthropicClient(fakeResponse);
        var costLogger = new InMemoryCostLogger();
        var service = new AgentRuntimeService(fakeClient, CreateRouter(), costLogger);

        var result = await service.ExecuteAsync(new AgentRuntimeRequest(
            TaskId: 1L,
            AgentName: "Agente de Aquisição",
            TenantId: "tenant_a",
            Complexity: ActivityComplexity.Planning,
            SystemPrompt: "Você é o Agente de Aquisição.",
            UserMessage: "Gere uma landing page para uma cafeteria."));

        Assert.Equal("claude-sonnet-5", fakeClient.LastRequest!.Model);
        Assert.Equal("landing page gerada", result.Text);
        Assert.Equal(200, result.InputTokens);
        Assert.Equal(80, result.OutputTokens);
    }

    [Fact]
    public async Task ExecuteAsync_QualquerChamada_RegistraCustoAssociadoATarefaETenant()
    {
        var fakeResponse = new AnthropicMessagesResponse(
            Id: "msg_1",
            Model: "claude-haiku-4-5-20251001",
            Content: new[] { new AnthropicContentBlock("text", "ok") },
            Usage: new AnthropicUsage(50, 20),
            StopReason: "end_turn");
        var fakeClient = new FakeAnthropicClient(fakeResponse);
        var costLogger = new InMemoryCostLogger();
        var service = new AgentRuntimeService(fakeClient, CreateRouter(), costLogger);

        await service.ExecuteAsync(new AgentRuntimeRequest(
            TaskId: 42L,
            AgentName: "tenant_b",
            TenantId: "tenant_b",
            Complexity: ActivityComplexity.Repetitive,
            SystemPrompt: "system",
            UserMessage: "user"));

        var totalTokens = await costLogger.GetAccumulatedTokensForTaskAsync(42L);
        Assert.Equal(70, totalTokens); // 50 + 20
    }

    [Fact]
    public async Task ExecuteAsync_RespostaComMultiplosBlocosDeTexto_ConcatenaNaOrdem()
    {
        var fakeResponse = new AnthropicMessagesResponse(
            Id: "msg_1",
            Model: "claude-sonnet-5",
            Content: new[]
            {
                new AnthropicContentBlock("text", "Parte um. "),
                new AnthropicContentBlock("text", "Parte dois.")
            },
            Usage: new AnthropicUsage(10, 10),
            StopReason: "end_turn");
        var fakeClient = new FakeAnthropicClient(fakeResponse);
        var service = new AgentRuntimeService(fakeClient, CreateRouter(), new InMemoryCostLogger());

        var result = await service.ExecuteAsync(new AgentRuntimeRequest(
            TaskId: 1L, AgentName: "tenant_a", TenantId: "tenant_a", Complexity: ActivityComplexity.Planning,
            SystemPrompt: "s", UserMessage: "u"));

        Assert.Equal("Parte um. Parte dois.", result.Text);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoAnthropicClientLancaExcecao_NaoRegistraCustoParcial()
    {
        // Documenta o comportamento esperado: se a chamada falha antes de retornar
        // uso de tokens, não deve existir registro de custo para essa tarefa —
        // evita contabilizar uma chamada que nunca completou.
        var fakeClient = new ThrowingAnthropicClient();
        var costLogger = new InMemoryCostLogger();
        var service = new AgentRuntimeService(fakeClient, CreateRouter(), costLogger);

        var taskFalha = 123L;
        await Assert.ThrowsAsync<AnthropicApiException>(() => service.ExecuteAsync(
            new AgentRuntimeRequest(
                TaskId: taskFalha, AgentName: "tenant_a", TenantId: "tenant_a", Complexity: ActivityComplexity.Planning,
                SystemPrompt: "s", UserMessage: "u")));

        var total = await costLogger.GetAccumulatedTokensForTaskAsync(taskFalha);
        Assert.Equal(0, total);
    }

    private sealed class ThrowingAnthropicClient : IAnthropicClient
    {
        public Task<AnthropicMessagesResponse> SendAsync(
            AnthropicMessagesRequest request, CancellationToken cancellationToken = default)
            => throw new AnthropicApiException("overloaded_error", "Servidor sobrecarregado.", 529);
    }
}
