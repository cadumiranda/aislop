using AutonomiaSaaS.Modules.AgentRuntime;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Tests;

internal sealed class FakeAgentRuntime : IAgentRuntime
{
    private readonly string _responseText;
    public AgentRuntimeRequest? LastRequest { get; private set; }

    public FakeAgentRuntime(string responseText) => _responseText = responseText;

    public Task<AgentRuntimeResult> ExecuteAsync(
        AgentRuntimeRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(new AgentRuntimeResult(_responseText, "claude-sonnet-5", 100, 200));
    }
}
