using AutonomiaSaaS.Modules.BusinessCore.Parts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;

/// <summary>
/// Decide QUEM deve ser notificado quando uma aprovação expira. Extraído como interface própria
/// porque a spec não documenta onde fica guardado "quem pediu essa tarefa" ou "quem é o dono do
/// negócio" no AgentTaskPart — então não dá pra saber o destinatário certo sem essa informação.
/// A implementação default (<see cref="AdministratorRoleRecipientResolver"/>) é um placeholder
/// razoável (notifica todo Administrator do tenant), não a solução final.
/// </summary>
public interface IApprovalExpirationRecipientResolver
{
    Task<IReadOnlyList<IUser>> ResolveRecipientsAsync(AgentTaskPart task, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default v0.1: notifica todos os usuários com role "Administrator" no tenant atual.
/// TROQUE isto assim que existir um campo real de "dono da aprovação" — por exemplo, se
/// AgentTaskPart vier a ganhar um RequestedByUserId, o resolver certo busca só aquele usuário
/// (e talvez também os Administrators, dependendo da política de negócio que vocês quiserem).
/// </summary>
public sealed class AdministratorRoleRecipientResolver : IApprovalExpirationRecipientResolver
{
    private readonly UserManager<IUser> _userManager;

    public AdministratorRoleRecipientResolver(UserManager<IUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<IUser>> ResolveRecipientsAsync(
        AgentTaskPart task, CancellationToken cancellationToken = default)
    {
        // GetUsersInRoleAsync não aceita CancellationToken (API do ASP.NET Core Identity) —
        // por isso não é passado adiante aqui, diferente do resto do método.
        var admins = await _userManager.GetUsersInRoleAsync("Administrator");
        return admins.ToList();
    }
}

/// <summary>
/// Substitui o LoggingApprovalExpirationNotifier como implementação registrada por padrão.
/// Usa OrchardCore.Notifications (INotificationService), que já resolve: canal de e-mail
/// pronto, extensível para Web Push/SMS via INotificationMethodProvider, e central de
/// notificações no admin com opt-in/out por usuário — nada disso precisou ser construído aqui.
///
/// PRÉ-REQUISITO: habilitar as features "OrchardCore.Notifications" e, para entrega de fato,
/// "OrchardCore.Notifications.Email" (ou o provider de canal que vocês escolherem) no
/// tenant/recipe. Sem isso, INotificationService existe mas não tem para onde mandar nada.
/// </summary>
public sealed class OrchardNotificationApprovalExpirationNotifier : IApprovalExpirationNotifier
{
    private readonly INotificationService _notificationService;
    private readonly IApprovalExpirationRecipientResolver _recipientResolver;
    private readonly ILogger<OrchardNotificationApprovalExpirationNotifier> _logger;

    public OrchardNotificationApprovalExpirationNotifier(
        INotificationService notificationService,
        IApprovalExpirationRecipientResolver recipientResolver,
        ILogger<OrchardNotificationApprovalExpirationNotifier> logger)
    {
        _notificationService = notificationService;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    public async Task NotifyExpiredAsync(AgentTaskPart task, CancellationToken cancellationToken = default)
    {
        var recipients = await _recipientResolver.ResolveRecipientsAsync(task, cancellationToken);

        if (recipients.Count == 0)
        {
            // Não é uma exceção — mas é um estado que vale saber que aconteceu, porque
            // significa que a expiração ficou muda para qualquer humano.
            _logger.LogWarning(
                "Tarefa {TaskId} expirou mas nenhum destinatário foi resolvido — ninguém foi notificado.",
                task.TaskId);
            return;
        }

        var notification = new NotificationMessage
        {
            Subject = $"Aprovação expirada: {task.AgentName}",
            Summary = $"A tarefa {task.TaskId} do agente '{task.AgentName}' expirou sem decisão " +
                   $"humana dentro do prazo ({task.ApprovalTimeout}). Ela foi movida para o " +
                   "estado Expirada e não será executada.",
        };

        // Uma falha ao notificar um destinatário não deve impedir a tentativa com os demais.
        foreach (var recipient in recipients)
        {
            try
            {
                await _notificationService.SendAsync(recipient, notification, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Falha ao enviar notificação de expiração da tarefa {TaskId} para o usuário {UserName}.",
                    task.TaskId, recipient.UserName);
            }
        }
    }
}
