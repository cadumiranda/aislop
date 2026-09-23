using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AutonomiaSaaS.Sandbox.Runner
{
    /// <summary>
    /// Serviço de segundo plano (Runner) que consome tarefas de agentes da fila de mensageria
    /// e as executa com segurança chamando o Claude Code CLI em modo headless e isolado.
    /// </summary>
    public class SandboxTaskProcessor : BackgroundService
    {
        private readonly ILogger<SandboxTaskProcessor> _logger;
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private const string InputQueueName = "agent_tasks_input";
        private const string OutputQueueName = "agent_tasks_output";

        public SandboxTaskProcessor(ILogger<SandboxTaskProcessor> logger)
        {
            _logger = logger;

            // Inicialização da conexão segura do RabbitMQ (ou Azure Service Bus via DI)
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnectionAsync(CancellationToken.None).Result;
            _channel = _connection.CreateChannelAsync().Result;

            _channel.QueueDeclareAsync(queue: InputQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueDeclareAsync(queue: OutputQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => _logger.LogInformation("Sandbox Runner parando..."));

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);

                _logger.LogInformation($"[Mensagem Recebida] Processando nova tarefa de agente.");

                try
                {
                    var taskEnvelope = JsonSerializer.Deserialize<AgentTaskEnvelope>(messageJson);
                    if (taskEnvelope != null)
                    {
                        // Executa a tarefa do agente de forma isolada na Sandbox
                        var result = await RunAgentTaskInSandboxAsync(taskEnvelope, stoppingToken);

                        // Publica o resultado de volta para o Orchard Core backend
                        await PublishResultToQueue(result);
                    }

                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro crítico ao processar tarefa na Sandbox.");
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsumeAsync(queue: InputQueueName, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Instancia um subprocesso seguro e isolado para executar a tarefa no Claude Code CLI.
        /// </summary>
        private async Task<AgentTaskResult> RunAgentTaskInSandboxAsync(AgentTaskEnvelope task, CancellationToken cancellationToken)
        {
            var result = new AgentTaskResult { TaskId = task.TaskId };

            // Mitigação de custo/loops: Define um timeout rígido por tarefa
            var timeoutMinutes = task.RiskLevel.ToLower() == "alto" ? 30 : 5;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

            try
            {
                // Comando: claude -p "instrucoes" --output-format json
                var startInfo = new ProcessStartInfo
                {
                    FileName = "claude",
                    Arguments = $"-p \"{EscapeArguments(task.Instructions)}\" --output-format json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = "/workspace" // Repositório de trabalho isolado do Tenant
                };

                using var process = new Process { StartInfo = startInfo };
                _logger.LogInformation($"Disparando Claude Code CLI para TaskId {task.TaskId}. Timeout: {timeoutMinutes} min.");

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Aguarda o término ou estouro de timeout (evita million-dollar bills por loops infinitos)
                var processTask = process.WaitForExitAsync(timeoutCts.Token);
                var completedTask = await Task.WhenAny(processTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));

                if (completedTask == processTask)
                {
                    result.ExitCode = process.ExitCode;
                    result.RawOutput = outputBuilder.ToString();
                    result.Errors = errorBuilder.ToString();
                    result.Success = process.ExitCode == 0;
                }
                else
                {
                    // Força o encerramento do processo em loop
                    _logger.LogWarning($"[TIMEOUT] Tarefa {task.TaskId} excedeu {timeoutMinutes} minutos e foi encerrada.");
                    process.Kill(entireProcessTree: true);

                    result.Success = false;
                    result.ExitCode = -1;
                    result.Errors = $"A tarefa ultrapassou o teto rígido de tempo ({timeoutMinutes} minutos) e foi abortada.";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors = $"Falha ao invocar o Claude CLI: {ex.Message}";
            }

            return result;
        }

        private async Task PublishResultToQueue(AgentTaskResult result)
        {
            var json = JsonSerializer.Serialize(result);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties();
            properties.Persistent = true;

            await _channel.BasicPublishAsync(exchange: "", routingKey: OutputQueueName, mandatory: true, basicProperties: properties, body: body, cancellationToken: CancellationToken.None);
        }

        private string EscapeArguments(string args)
        {
            return args.Replace("\"", "\\\"");
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }

    public class AgentTaskEnvelope
    {
        public long TaskId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = "baixo";
    }

    public class AgentTaskResult
    {
        public long TaskId { get; set; }
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string RawOutput { get; set; } = string.Empty;
        public string Errors { get; set; } = string.Empty;
    }
}