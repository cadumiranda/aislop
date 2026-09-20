using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using AutonomiaSaaS.Modules.RiskGate.AuditTrail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;

/// <summary>
/// Fecha a lacuna [PENDENTE] da spec v2.0, seção 5: "AgentTaskStore.GetExpiredApprovalsAsync
/// existe e funciona, mas nenhum IBackgroundTask chama isso em ciclo ainda".
///
/// Roda a cada 5 minutos, por tenant (mecanismo nativo do Orchard Core — não escrevi nenhum
/// loop manual de tenant aqui). Cada tarefa expirada é transicionada individualmente: uma
/// falha em uma tarefa não deve impedir as outras de expirarem também (mesmo princípio de
/// "uma imagem por família de ferramenta" do módulo de sandbox — isolar a unidade de falha).
/// </summary>
[BackgroundTask(
    Schedule = "*/5 * * * *",
    Description = "Expira tarefas de alto risco cuja janela de aprovação venceu, e notifica.",
    // LockTimeout: quanto tempo esta instância espera para conseguir o lock antes de desistir
    // desta execução (não é o timeout do trabalho em si).
    // LockExpiration: por quanto tempo o lock fica valendo — precisa ser maior que o tempo
    // esperado de DoWorkAsync (poucas tarefas, execução rápida), mas não tão longo que, se
    // esta instância cair no meio, as outras fiquem bloqueadas por muito tempo.
    // IMPORTANTE: isso só impede execução duplicada entre instâncias se um provedor de lock
    // distribuído estiver configurado (ex: OrchardCore.Redis) — sem isso, o lock é só local a
    // cada processo, e com múltiplas instâncias a tarefa ainda pode rodar em paralelo em cada
    // uma. Decisão de infra a confirmar antes de escalar horizontalmente.
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class ApprovalExpirationBackgroundTask : IBackgroundTask
{
    private const string AuditTrailCategory = "AutonomiaSaaS.RiskGate";

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var agentTaskStore = serviceProvider.GetRequiredService<IAgentTaskStore>();
        var notifier = serviceProvider.GetRequiredService<IApprovalExpirationNotifier>();
        var auditTrailRecorder = serviceProvider.GetRequiredService<IAuditTrailRecorder>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApprovalExpirationBackgroundTask>>();

        IReadOnlyList<AgentTaskPart> expiredTasks;
        try
        {
            expiredTasks = await agentTaskStore.GetExpiredApprovalsAsync(TimeProvider.System.GetUtcNow(), cancellationToken);
        }
        catch (Exception ex)
        {
            // Falha ao consultar é diferente de "nenhuma tarefa expirada" — não engolir isso
            // como se fosse uma lista vazia legítima.
            logger.LogError(ex, "Falha ao consultar aprovações expiradas.");
            return;
        }

        if (expiredTasks.Count == 0)
        {
            return;
        }

        logger.LogInformation("{Count} tarefa(s) de aprovação expiraram e serão transicionadas.", expiredTasks.Count);

        foreach (var task in expiredTasks)
        {
            await ExpireOneAsync(task, agentTaskStore, notifier, auditTrailRecorder, logger, cancellationToken);
        }
    }

    private static async Task ExpireOneAsync(
        AgentTaskPart task,
        IAgentTaskStore agentTaskStore,
        IApprovalExpirationNotifier notifier,
        IAuditTrailRecorder auditTrailRecorder,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // AguardandoAprovacao -> Expirada é uma transição direta (doc de arquitetura,
            // diagrama de estados) — sem rollback_action, porque a tarefa nunca chegou a
            // executar nada. Rollback só existe no ramo de auto-execução.
            await agentTaskStore.TransitionAsync(task.TaskId.Value, AgentTaskStatus.Expirada, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao transicionar a tarefa {TaskId} para Expirada.", task.TaskId);
            return; // não notifica nem audita uma expiração que não foi de fato persistida
        }

        // Best-effort, na mesma ordem de importância que a notificação: a máquina de estados
        // já está correta neste ponto, então nem falha de auditoria nem falha de notificação
        // deveriam reverter isso ou travar as próximas tarefas da lista.
        await auditTrailRecorder.RecordAsync<ApprovalExpiredAuditEvent>(
            name: "ApprovalExpired",
            category: AuditTrailCategory,
            correlationId: task.TaskId.Value.ToString(),
            eventItem: new ApprovalExpiredAuditEvent { TaskId = task.TaskId.Value, AgentName = task.AgentName },
            cancellationToken: cancellationToken);

        try
        {
            await notifier.NotifyExpiredAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            // A transição de estado já aconteceu e é o que importa para a máquina de estados
            // ficar correta — uma falha de notificação não deveria reverter isso nem travar
            // as próximas tarefas da lista.
            logger.LogError(ex, "Tarefa {TaskId} expirou, mas a notificação falhou.", task.TaskId);
        }
    }
}