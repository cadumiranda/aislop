using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.ApprovalExpiration.Tests;

public sealed class FakeUser : IUser
{
    private string? _userName;
    public required string UserId { get; init; }
    public string UserName { get => string.IsNullOrEmpty(_userName) ? UserId : _userName!; set => _userName = value; }
    public string Email { get; set; } = string.Empty;
}

public sealed class FakeRecipientResolver : IApprovalExpirationRecipientResolver
{
    public List<IUser> RecipientsToReturn { get; set; } = new();

    public Task<IReadOnlyList<IUser>> ResolveRecipientsAsync(
        AgentTaskPart task, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IUser>>(RecipientsToReturn);
}

public sealed class FakeNotificationService : INotificationService
{
    public List<(IUser User, INotificationMessage Notification)> SentNotifications { get; } = new();
    public HashSet<string> UserIdsThatThrow { get; } = new();

    public Task<NotificationSendResult> SendAsync(object notify, INotificationMessage message, CancellationToken cancellationToken = default)
    {
        var user = notify as IUser;
        if (user == null)
            throw new ArgumentNullException(nameof(notify));

        if (UserIdsThatThrow.Contains(user.UserName))
        {
            throw new InvalidOperationException($"falha simulada ao notificar {user.UserName}");
        }
        SentNotifications.Add((user, message));
        return Task.FromResult(new NotificationSendResult());
    }
}

public sealed class OrchardNotificationApprovalExpirationNotifierTests
{
    private static AgentTaskPart MakeTask() => (
        new AgentTaskPart
        {
            ContentItem = new OrchardCore.ContentManagement.ContentItem { Id = new Random().NextInt64(1000000, 2000000) },
            AgentName = "acquisition_agent",
            Status = AgentTaskStatus.AguardandoAprovacao,
            ApprovalTimeout = DateTimeOffset.UtcNow.AddHours(-1),
        });

    [Fact]
    public async Task NotifyExpiredAsync_SendsToEveryResolvedRecipient()
    {
        var notificationService = new FakeNotificationService();
        var resolver = new FakeRecipientResolver
        {
            RecipientsToReturn = new List<IUser>
            {
                new FakeUser { UserId = "u1" },
                new FakeUser { UserId = "u2" },
            },
        };
        var notifier = new OrchardNotificationApprovalExpirationNotifier(
            notificationService, resolver, NullLogger<OrchardNotificationApprovalExpirationNotifier>.Instance);

        await notifier.NotifyExpiredAsync(MakeTask());

        Assert.Equal(2, notificationService.SentNotifications.Count);
        Assert.Contains(notificationService.SentNotifications, s => s.User.UserName == "u1");
        Assert.Contains(notificationService.SentNotifications, s => s.User.UserName == "u2");
    }

    [Fact]
    public async Task NotifyExpiredAsync_DoesNotThrow_WhenNoRecipientsResolved()
    {
        var notificationService = new FakeNotificationService();
        var resolver = new FakeRecipientResolver { RecipientsToReturn = new List<IUser>() };
        var notifier = new OrchardNotificationApprovalExpirationNotifier(
            notificationService, resolver, NullLogger<OrchardNotificationApprovalExpirationNotifier>.Instance);

        await notifier.NotifyExpiredAsync(MakeTask());

        Assert.Empty(notificationService.SentNotifications);
    }

    [Fact]
    public async Task NotifyExpiredAsync_ContinuesToNextRecipient_WhenOneSendFails()
    {
        var notificationService = new FakeNotificationService();
        notificationService.UserIdsThatThrow.Add("u1");
        var resolver = new FakeRecipientResolver
        {
            RecipientsToReturn = new List<IUser>
            {
                new FakeUser { UserId = "u1" },
                new FakeUser { UserId = "u2" },
            },
        };
        var notifier = new OrchardNotificationApprovalExpirationNotifier(
            notificationService, resolver, NullLogger<OrchardNotificationApprovalExpirationNotifier>.Instance);

        // Não deve propagar mesmo com falha no primeiro destinatário.
        await notifier.NotifyExpiredAsync(MakeTask());

        Assert.Single(notificationService.SentNotifications);
        Assert.Equal("u2", notificationService.SentNotifications[0].User.UserName);
    }
}

/// <summary>
/// Guarda de regressão: garante que ninguém remova o lock distribuído por engano numa futura
/// edição do atributo. Não testa comportamento de lock em si (isso é do Orchard Core),
/// só que os valores continuam configurados.
/// </summary>
public sealed class ApprovalExpirationBackgroundTaskAttributeTests
{
    [Fact]
    public void BackgroundTaskAttribute_HasLockConfigured()
    {
        var attribute = typeof(ApprovalExpirationBackgroundTask)
            .GetCustomAttributes(typeof(BackgroundTaskAttribute), inherit: false)
            .Cast<BackgroundTaskAttribute>()
            .Single();

        Assert.True(attribute.LockTimeout > 0, "LockTimeout deveria estar configurado (>0) para permitir lock distribuído.");
        Assert.True(attribute.LockExpiration > attribute.LockTimeout, "LockExpiration deveria ser maior que LockTimeout.");
    }
}
