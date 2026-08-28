using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutonomiaSaaS.Modules.AgentRuntime.Models;
using Microsoft.Extensions.Options;

namespace AutonomiaSaaS.Modules.AgentRuntime;

/// <summary>
/// Implementação HTTP de IAnthropicClient. Espera receber um HttpClient já
/// configurado via IHttpClientFactory (ver DependencyInjection/ServiceCollectionExtensions),
/// com BaseAddress e headers padrão já aplicados — esta classe não configura
/// o HttpClient sozinha, só o usa.
/// </summary>
public sealed class AnthropicClient : IAnthropicClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AnthropicClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AnthropicMessagesResponse> SendAsync(
        AnthropicMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .PostAsJsonAsync("/v1/messages", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content
                .ReadFromJsonAsync<AnthropicMessagesResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return result ?? throw new AnthropicApiException(
                errorType: "empty_response",
                message: "A API retornou 200 mas o corpo veio vazio ou não pôde ser desserializado.");
        }

        // Erro reportado pela própria API (não falha de transporte) — tenta ler
        // o corpo estruturado antes de estourar uma exceção genérica.
        AnthropicErrorResponse? errorBody = null;
        try
        {
            errorBody = await response.Content
                .ReadFromJsonAsync<AnthropicErrorResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Corpo não veio no formato esperado — segue com errorBody nulo,
            // tratado abaixo com uma mensagem genérica em vez de propagar
            // o erro de parsing, que esconderia a causa real (status HTTP).
        }

        throw new AnthropicApiException(
            errorType: errorBody?.Error.Type ?? "unknown_error",
            message: errorBody?.Error.Message
                ?? $"Chamada à API da Anthropic falhou com status {(int)response.StatusCode}.",
            httpStatusCode: (int)response.StatusCode);
    }
}
