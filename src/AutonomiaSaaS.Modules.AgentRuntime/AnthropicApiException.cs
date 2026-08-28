namespace AutonomiaSaaS.Modules.AgentRuntime;

/// <summary>
/// Erro reportado pela própria API da Anthropic (não erro de rede/transporte).
/// Separar isso de HttpRequestException importa porque o AcquisitionAgent
/// (e qualquer agente futuro) precisa decidir diferente para cada caso:
/// erro de rede pode justificar retry automático dentro da mesma atividade
/// de workflow; erro de API (ex: chave inválida, rate limit) normalmente não deve
/// ser reexecutado sem intervenção, e deve virar uma tarefa em status de falha
/// visível no painel, não um retry silencioso.
/// </summary>
public sealed class AnthropicApiException : Exception
{
    public string ErrorType { get; }
    public int? HttpStatusCode { get; }

    public AnthropicApiException(string errorType, string message, int? httpStatusCode = null)
        : base(message)
    {
        ErrorType = errorType;
        HttpStatusCode = httpStatusCode;
    }
}
