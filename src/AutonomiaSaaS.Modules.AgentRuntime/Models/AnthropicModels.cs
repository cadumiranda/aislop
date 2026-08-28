using System.Text.Json.Serialization;

namespace AutonomiaSaaS.Modules.AgentRuntime.Models;

/// <summary>
/// Papel do autor de uma mensagem, conforme a API /v1/messages da Anthropic.
/// </summary>
public static class AnthropicRole
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// Uma mensagem simples de texto. A Fase 1 não usa blocos de ferramenta
/// (tool_use / tool_result) nem imagem — só texto. Esses tipos são deixados
/// de fora de propósito para manter o payload inicial pequeno; o modelo de
/// dados pode crescer quando um agente futuro precisar de tool calling real.
/// </summary>
public sealed record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

public sealed record AnthropicMessagesRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
    [property: JsonPropertyName("system")] string? System = null
);

public sealed record AnthropicContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text
);

public sealed record AnthropicUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens
);

public sealed record AnthropicMessagesResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock> Content,
    [property: JsonPropertyName("usage")] AnthropicUsage Usage,
    [property: JsonPropertyName("stop_reason")] string? StopReason
);

/// <summary>
/// Erro retornado pela API da Anthropic no formato { "type": "error", "error": { ... } }.
/// Usado para diferenciar falha de rede de falha reportada pela própria API
/// (ex: rate limit, chave inválida, modelo inexistente).
/// </summary>
public sealed record AnthropicErrorResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("error")] AnthropicErrorDetail Error
);

public sealed record AnthropicErrorDetail(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message
);
