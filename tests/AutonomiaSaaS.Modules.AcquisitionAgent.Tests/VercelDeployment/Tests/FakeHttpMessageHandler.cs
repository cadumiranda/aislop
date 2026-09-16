using System.Net;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Tests;

/// <summary>
/// Fake de HttpMessageHandler — grava cada requisição e devolve respostas de uma fila
/// configurada (uma por chamada, na ordem; a última é reusada se a fila acabar, útil para
/// polling onde só a resposta final muda).
///
/// IMPORTANTE: guarda os dados brutos (status + corpo), não instâncias de HttpResponseMessage.
/// VercelDeploymentClient faz `using var response = ...` a cada chamada — se a mesma instância
/// fosse reaproveitada (ex: via Peek() num cenário de polling), a segunda leitura encontraria um
/// objeto já descartado pelo `using` da chamada anterior e lançaria ObjectDisposedException.
/// Por isso SendAsync sempre constrói uma HttpResponseMessage NOVA a partir dos dados guardados.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode StatusCode, string JsonBody)> _responses = new();
    public List<HttpRequestMessage> Requests { get; } = new();

    public void Enqueue(HttpStatusCode statusCode, string jsonBody)
    {
        _responses.Enqueue((statusCode, jsonBody));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                "FakeHttpMessageHandler: nenhuma resposta enfileirada para esta chamada — " +
                "chame Enqueue() o suficiente para cobrir todas as requisições esperadas pelo teste.");
        }

        var (statusCode, jsonBody) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
