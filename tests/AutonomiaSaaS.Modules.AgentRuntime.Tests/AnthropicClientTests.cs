using System.Net;
using AutonomiaSaaS.Modules.AgentRuntime.Models;
using Xunit;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

public class AnthropicClientTests
{
    private static AnthropicClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
        return new AnthropicClient(httpClient);
    }

    [Fact]
    public async Task SendAsync_QuandoRespostaOk_RetornaConteudoDesserializado()
    {
        var json = """
        {
          "id": "msg_123",
          "model": "claude-sonnet-5",
          "content": [{ "type": "text", "text": "Olá, mundo" }],
          "usage": { "input_tokens": 10, "output_tokens": 5 },
          "stop_reason": "end_turn"
        }
        """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-5",
            MaxTokens: 100,
            Messages: new[] { new AnthropicMessage(AnthropicRole.User, "oi") });

        var result = await client.SendAsync(request);

        Assert.Equal("msg_123", result.Id);
        Assert.Single(result.Content);
        Assert.Equal("Olá, mundo", result.Content[0].Text);
        Assert.Equal(10, result.Usage.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
    }

    [Fact]
    public async Task SendAsync_QuandoApiRetornaErro_LancaAnthropicApiExceptionComDetalhe()
    {
        var json = """
        {
          "type": "error",
          "error": { "type": "rate_limit_error", "message": "Limite de requisições excedido." }
        }
        """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.TooManyRequests, json);
        var client = CreateClient(handler);

        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-5",
            MaxTokens: 100,
            Messages: new[] { new AnthropicMessage(AnthropicRole.User, "oi") });

        var exception = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.SendAsync(request));

        Assert.Equal("rate_limit_error", exception.ErrorType);
        Assert.Equal(429, exception.HttpStatusCode);
        Assert.Contains("Limite de requisições", exception.Message);
    }

    [Fact]
    public async Task SendAsync_QuandoCorpoDeErroNaoEstaNoFormatoEsperado_NaoLancaJsonException()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(
            HttpStatusCode.InternalServerError, "<html>gateway timeout</html>");
        var client = CreateClient(handler);

        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-5",
            MaxTokens: 100,
            Messages: new[] { new AnthropicMessage(AnthropicRole.User, "oi") });

        // Não deve vazar a JsonException do parsing malsucedido — deve virar
        // AnthropicApiException com mensagem genérica baseada no status code.
        var exception = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.SendAsync(request));

        Assert.Equal(500, exception.HttpStatusCode);
    }
}
