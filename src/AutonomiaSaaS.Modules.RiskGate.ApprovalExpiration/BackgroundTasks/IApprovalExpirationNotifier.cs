using AutonomiaSaaS.Modules.BusinessCore.Parts;
using Microsoft.Extensions.Logging;

namespace AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;

/// <summary>
/// Ponto de extensão para "e notifica" (doc de arquitetura, linha 116). Nenhum canal real
/// (e-mail/WhatsApp/push) foi especificado na spec, então isto fica como interface — trocar
/// a implementação registrada em Startup.cs pelo canal real quando ele existir, sem tocar no
/// background task em si.
/// </summary>
public interface IApprovalExpirationNotifier
{
    Task NotifyExpiredAsync(AgentTaskPart task, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação padrão v0.1: só loga. Suficiente para não perder o evento silenciosamente
/// enquanto o canal real não é decidido, mas não cumpre "notifica" no sentido de alcançar
/// um humano — não trate isso como solução final.
/// </summary>
public sealed class LoggingApprovalExpirationNotifier : IApprovalExpirationNotifier
{
    private readonly ILogger<LoggingApprovalExpirationNotifier> _logger;

    public LoggingApprovalExpirationNotifier(ILogger<LoggingApprovalExpirationNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyExpiredAsync(AgentTaskPart task, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Aprovação expirada sem decisão humana: tarefa {TaskId} (agente {AgentName}), timeout em {ApprovalTimeout}.",
            task.ContentItem.Id, task.AgentName, task.ApprovalTimeout);
        return Task.CompletedTask;
    }
}
