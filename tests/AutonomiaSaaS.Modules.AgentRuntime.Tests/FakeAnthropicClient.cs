using AutonomiaSaaS.Modules.AgentRuntime.Models;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

/// <summary>
/// Fake de IAnthropicClient para testar AgentRuntimeService sem passar pela
/// camada HTTP — os testes de AnthropicClient já cobrem a parte de rede/parsing;
/// aqui o foco é a orquestração entre roteador de modelo, cliente e custo.
/// </summary>
internal sealed class FakeAnthropicClient : IAnthropicClient
{
    private readonly AnthropicMessagesResponse _response;
    public AnthropicMessagesRequest? LastRequest { get; private set; }

    public FakeAnthropicClient(AnthropicMessagesResponse response)
    {
        _response = response;
    }

    public Task<AnthropicMessagesResponse> SendAsync(
        AnthropicMessagesRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(_response);
    }
}
