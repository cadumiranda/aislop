using AutonomiaSaaS.Modules.AgentRuntime.CostLogger;
using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;
using AutonomiaSaaS.Modules.AgentRuntime.Models;

namespace AutonomiaSaaS.Modules.AgentRuntime;

public sealed class AgentRuntimeService : IAgentRuntime
{
    private readonly IAnthropicClient _client;
    private readonly IModelRouter _modelRouter;
    private readonly ICostLogger _costLogger;

    public AgentRuntimeService(
        IAnthropicClient client,
        IModelRouter modelRouter,
        ICostLogger costLogger)
    {
        _client = client;
        _modelRouter = modelRouter;
        _costLogger = costLogger;
    }

    public async Task<AgentRuntimeResult> ExecuteAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = _modelRouter.ResolveModel(request.Complexity);

        var apiRequest = new AnthropicMessagesRequest(
            Model: model,
            MaxTokens: request.MaxTokens,
            Messages: new[] { new AnthropicMessage(AnthropicRole.User, request.UserMessage) },
            System: request.SystemPrompt);

        var response = await _client.SendAsync(apiRequest, cancellationToken).ConfigureAwait(false);

        await _costLogger.LogAsync(
            new ModelCallCost(
                TaskId: request.TaskId,
                AgentName: request.AgentName,
                TenantId: request.TenantId,
                Model: response.Model,
                Complexity: request.Complexity,
                InputTokens: response.Usage.InputTokens,
                OutputTokens: response.Usage.OutputTokens,
                OccurredAt: DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        // A Fase 1 só lida com resposta de texto simples (sem tool_use) —
        // concatena todos os blocos de texto retornados, na ordem em que vieram.
        var text = string.Join(
            separator: string.Empty,
            values: response.Content
                .Where(block => block.Type == "text" && block.Text is not null)
                .Select(block => block.Text));

        return new AgentRuntimeResult(
            Text: text,
            ModelUsed: response.Model,
            InputTokens: response.Usage.InputTokens,
            OutputTokens: response.Usage.OutputTokens);
    }
}
