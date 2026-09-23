namespace AutonomiaSaaS.Modules.Sandbox.Abstractions;

/// <summary>
/// Describes one isolated execution: which image to run, what command to run inside it,
/// which files to seed the workspace with, and the resource/time limits to enforce.
///
/// Um agente nunca chama o Docker diretamente — ele monta um <see cref="SandboxExecutionRequest"/>
/// e entrega pra fila (<see cref="ISandboxExecutionQueue"/>). Isso é o equivalente, em código,
/// do "agente propõe ação" da seção 4 do doc de arquitetura: a diferença é que aqui não existe
/// classificação de risco alto/baixo — toda execução de ferramenta passa pelo sandbox, sempre.
/// </summary>
public sealed record SandboxExecutionRequest
{
    /// <summary>Identificador da AgentTask (ou equivalente) que originou esta execução — para correlação nos logs.</summary>
    public required long TaskId { get; init; }

    /// <summary>Nome do agente dono da execução (ex: "product_agent"). Usado para escolher a imagem certa por domínio.</summary>
    public required string AgentName { get; init; }

    /// <summary>Imagem Docker a usar. Deve já existir localmente ou em um registry acessível — este módulo nunca faz build on-the-fly.</summary>
    public required string Image { get; init; }

    /// <summary>Comando a rodar dentro do container, já tokenizado (evita problemas de shell-escaping).</summary>
    public required IReadOnlyList<string> Command { get; init; }

    /// <summary>Arquivos a materializar no workspace antes de rodar o comando. Chave = caminho relativo, valor = conteúdo em texto.</summary>
    public IReadOnlyDictionary<string, string> InputFiles { get; init; } = new Dictionary<string, string>();

    /// <summary>Padrões glob relativos ao workspace a capturar como saída depois da execução (ex: "dist/**", "*.log").</summary>
    public IReadOnlyList<string> OutputPatterns { get; init; } = Array.Empty<string>();

    public SandboxResourceLimits Limits { get; init; } = SandboxResourceLimits.Default;

    /// <summary>Tempo máximo de execução. Depois disso o host mata o processo/container, sem exceção.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Rede que o container pode acessar. "none" por padrão — a chamada ao modelo (Anthropic) acontece
    /// no host via AgentRuntime, não daqui de dentro. Só mude isso se a ferramenta específica precisar
    /// (ex: "npm install" precisa de rede pra baixar pacotes).
    /// </summary>
    public string NetworkMode { get; init; } = "none";
}

public sealed record SandboxResourceLimits
{
    public string MemoryLimit { get; init; } = "512m";
    public double CpuLimit { get; init; } = 1.0;
    public int PidsLimit { get; init; } = 128;

    public static SandboxResourceLimits Default { get; } = new();
}

public sealed record SandboxExecutionResult
{
    public required long TaskId { get; init; }
    public required bool Succeeded { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;

    /// <summary>Conteúdo dos arquivos capturados via OutputPatterns. Chave = caminho relativo.</summary>
    public IReadOnlyDictionary<string, string> OutputFiles { get; init; } = new Dictionary<string, string>();

    public bool TimedOut { get; init; }
    public string? FailureReason { get; init; }
}

/// <summary>
/// Executa uma requisição isolada e devolve o resultado. Implementações não devem lançar exceção
/// para falhas *esperadas* de execução (comando saiu com código != 0) — isso vai em
/// <see cref="SandboxExecutionResult.Succeeded"/>. Exceção é reservada para falha de infraestrutura
/// (Docker indisponível, imagem não encontrada).
/// </summary>
public interface ISandboxRunner
{
    Task<SandboxExecutionResult> RunAsync(SandboxExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fila persistida entre a proposta de execução e o worker que efetivamente roda o container.
/// A v0.1 usa uma fila em memória (ver <c>InMemorySandboxExecutionQueue</c>) — suficiente para um
/// host único, mesma limitação que o resto da Fase 1 já assume (ver ICostLogger in-memory na spec v2.0).
/// Persistir isso (ex: como Content Type, no mesmo padrão de AgentTask) é o próximo passo óbvio
/// quando isso precisar sobreviver a um restart do processo.
/// </summary>
public interface ISandboxExecutionQueue
{
    ValueTask EnqueueAsync(SandboxExecutionRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<SandboxExecutionRequest> DequeueAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Publicado pelo hosted service quando uma execução termina, para quem propôs a tarefa (ex: um
/// futuro IApprovedTaskExecutor do Agente de Produto) saber que o resultado está pronto.
/// Deliberadamente simples (evento em memória) em vez de inventar um barramento de mensagens —
/// mesmo princípio de "não somar peça nova de infra sem necessidade provada" da v2.0.
/// </summary>
public interface ISandboxExecutionResultSink
{
    Task PublishAsync(SandboxExecutionResult result, CancellationToken cancellationToken = default);
}
