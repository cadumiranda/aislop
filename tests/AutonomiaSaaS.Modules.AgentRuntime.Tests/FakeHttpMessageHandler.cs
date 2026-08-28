using System.Net;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

/// <summary>
/// Handler que devolve uma resposta fixa (ou uma sequência configurável) sem
/// fazer nenhuma chamada de rede real — os testes deste módulo nunca devem
/// depender de acesso à internet nem de uma chave de API válida.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static FakeHttpMessageHandler ReturningJson(HttpStatusCode statusCode, string json)
        => new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_responder(request));
    }
}
