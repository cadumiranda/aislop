using AutonomiaSaaS.Modules.Sandbox.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutonomiaSaaS.Modules.Sandbox.Execution;

/// <summary>
/// É o "IHostedService consumindo o resultado" desenhado na v1.0 e citado na spec v2.0 (seção 4)
/// como a direção que continua certa, só não construída ainda. Processa uma requisição por vez
/// deliberadamente na v0.1 — paralelismo sem um limite de recursos agregado por host é o mesmo tipo
/// de problema de "custo sem teto" que a seção 8 do doc de arquitetura already alerta para modelo;
/// aqui seria CPU/RAM do host em vez de tokens.
/// </summary>
public sealed class SandboxExecutionHostedService : BackgroundService
{
    private readonly ISandboxExecutionQueue _queue;
    private readonly ISandboxRunner _runner;
    private readonly ISandboxExecutionResultSink _resultSink;
    private readonly ILogger<SandboxExecutionHostedService> _logger;

    public SandboxExecutionHostedService(
        ISandboxExecutionQueue queue,
        ISandboxRunner runner,
        ISandboxExecutionResultSink resultSink,
        ILogger<SandboxExecutionHostedService> logger)
    {
        _queue = queue;
        _runner = runner;
        _resultSink = resultSink;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                var result = await _runner.RunAsync(request, stoppingToken);
                await _resultSink.PublishAsync(result, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown normal do host — não é uma falha de execução a registrar.
                break;
            }
            catch (Exception ex)
            {
                // Falha de infraestrutura (ex: Docker daemon fora do ar) — diferente de um comando
                // que falhou dentro do container, que já vem como Succeeded=false do runner.
                _logger.LogError(ex, "Sandbox: falha de infraestrutura executando tarefa {TaskId}", request.TaskId);
                await _resultSink.PublishAsync(new SandboxExecutionResult
                {
                    TaskId = request.TaskId,
                    Succeeded = false,
                    ExitCode = -1,
                    FailureReason = $"Falha de infraestrutura do sandbox: {ex.Message}",
                }, stoppingToken);
            }
        }
    }
}

/// <summary>
/// Implementação mínima v0.1: só loga o resultado. Um consumidor real (ex: um futuro
/// IApprovedTaskExecutor do Agente de Produto) deve substituir isso por algo que atualize o
/// AgentTask correspondente via AgentTaskStore.TransitionAsync — não foi feito aqui porque esse
/// contrato vive em BusinessCore, que não foi anexado nesta conversa.
/// </summary>
public sealed class LoggingSandboxExecutionResultSink : ISandboxExecutionResultSink
{
    private readonly ILogger<LoggingSandboxExecutionResultSink> _logger;

    public LoggingSandboxExecutionResultSink(ILogger<LoggingSandboxExecutionResultSink> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(SandboxExecutionResult result, CancellationToken cancellationToken = default)
    {
        if (result.Succeeded)
        {
            _logger.LogInformation("Sandbox: tarefa {TaskId} concluída com sucesso", result.TaskId);
        }
        else
        {
            _logger.LogWarning(
                "Sandbox: tarefa {TaskId} falhou (timeout={TimedOut}, motivo={Reason})",
                result.TaskId, result.TimedOut, result.FailureReason);
        }
        return Task.CompletedTask;
    }
}
