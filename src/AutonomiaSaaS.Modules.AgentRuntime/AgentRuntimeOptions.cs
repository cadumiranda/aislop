namespace AutonomiaSaaS.Modules.AgentRuntime;

/// <summary>
/// Configuração do módulo AgentRuntime. A ApiKey nunca deve vir de appsettings.json
/// em texto puro — em produção, ela é resolvida via Azure Key Vault e injetada
/// como variável de ambiente/secret do host, nunca commitada no repositório.
/// </summary>
public sealed class AgentRuntimeOptions
{
    public const string SectionName = "AgentRuntime";

    /// <summary>
    /// Chave de API da Anthropic. Ler de variável de ambiente ou Key Vault,
    /// nunca de arquivo de configuração versionado.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL da API. Parametrizado para permitir apontar para um proxy
    /// interno de custo/observabilidade no futuro, sem mudar código.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>
    /// Versão da API exigida no header anthropic-version.
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Modelo usado para atividades de planejamento/raciocínio complexo
    /// (seção 8 do documento de arquitetura: roteamento por complexidade).
    /// </summary>
    public string PlanningModel { get; set; } = "claude-sonnet-5";

    /// <summary>
    /// Modelo usado para atividades repetitivas/determinísticas de baixo custo.
    /// </summary>
    public string RepetitiveModel { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>
    /// Timeout de chamada HTTP em segundos. Curto de propósito: uma atividade de
    /// workflow travada não deve segurar o worker indefinidamente.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
