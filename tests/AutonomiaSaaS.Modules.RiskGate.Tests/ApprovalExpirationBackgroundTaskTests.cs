using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using AutonomiaSaaS.Modules.RiskGate.AuditTrail;
using AutonomiaSaaS.Modules.RiskGate.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

public sealed class FakeApprovalExpirationNotifier : IApprovalExpirationNotifier
{
    public List<long> NotifiedTaskIds { get; } = new();
    public HashSet<long> TaskIdsThatThrowOnNotify { get; } = new();

    public Task NotifyExpiredAsync(AgentTaskPart task, CancellationToken cancellationToken = default)
    {
        if (!task.TaskId.HasValue || TaskIdsThatThrowOnNotify.Contains(task.TaskId.Value))
        {
            throw new InvalidOperationException($"falha simulada ao notificar {task.TaskId}");
        }
        NotifiedTaskIds.Add(task.TaskId.Value);
        return Task.CompletedTask;
    }
}

public sealed class FakeAuditTrailRecorder : IAuditTrailRecorder
{
    public List<(string Name, string Category, string CorrelationId, object? EventItem)> RecordedEvents { get; } = new();

    public Task RecordAsync<T>(string name, string category, string correlationId, T eventItem, CancellationToken cancellationToken = default) where T : class, new()
    {
        RecordedEvents.Add((name, category, correlationId, eventItem));
        return Task.CompletedTask;
    }
}

public sealed class ApprovalExpirationBackgroundTaskTests
{
    private static AgentTaskPart MakePart(long id, string agentName = "acquisition_agent") => new AgentTaskPart()
    {
        ContentItem = new OrchardCore.ContentManagement.ContentItem { Id = id },
        AgentName = agentName,
        Status = AgentTaskStatus.AguardandoAprovacao,
        ApprovalTimeout = DateTimeOffset.UtcNow.AddHours(-1),
    };

    private static IServiceProvider BuildServiceProvider(
        FakeAgentTaskStore store, FakeApprovalExpirationNotifier notifier, FakeAuditTrailRecorder? auditTrailRecorder = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentTaskStore>(store);
        services.AddSingleton<IApprovalExpirationNotifier>(notifier);
        services.AddSingleton<IAuditTrailRecorder>(auditTrailRecorder ?? new FakeAuditTrailRecorder());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DoWorkAsync_TransitionsAndNotifies_ForEachExpiredTask()
    {
        var store = new FakeAgentTaskStore
        {
            ExpiredTasksToReturn = new List<AgentTaskPart>
            {
                MakePart(1),
                MakePart(2),
            },
        };
        var notifier = new FakeApprovalExpirationNotifier();
        var provider = BuildServiceProvider(store, notifier);

        await new ApprovalExpirationBackgroundTask().DoWorkAsync(provider, CancellationToken.None);

        Assert.Equal(new[] { (long)1, (long)2 }, store.TransitionedTaskIds);
        Assert.Equal(new[] { (long)1, (long)2 }, notifier.NotifiedTaskIds);
    }

    [Fact]
    public async Task DoWorkAsync_DoesNothing_WhenNoExpiredTasks()
    {
        var store = new FakeAgentTaskStore { ExpiredTasksToReturn = new List<AgentTaskPart>() };
        var notifier = new FakeApprovalExpirationNotifier();
        var provider = BuildServiceProvider(store, notifier);

        await new ApprovalExpirationBackgroundTask().DoWorkAsync(provider, CancellationToken.None);

        Assert.Empty(store.TransitionedTaskIds);
        Assert.Empty(notifier.NotifiedTaskIds);
    }

    [Fact]
    public async Task DoWorkAsync_DoesNotThrow_WhenGetExpiredApprovalsFails()
    {
        var store = new FakeAgentTaskStore { ThrowOnGetExpired = true };
        var notifier = new FakeApprovalExpirationNotifier();
        var provider = BuildServiceProvider(store, notifier);

        // Não deve propagar — uma falha de consulta não deveria derrubar o schedule do
        // Orchard Core nem impedir a próxima execução em 5 minutos.
        await new ApprovalExpirationBackgroundTask().DoWorkAsync(provider, CancellationToken.None);
    }

    [Fact]
    public async Task DoWorkAsync_ContinuesToNextTask_WhenOneTransitionFails()
    {
        var store = new FakeAgentTaskStore
        {
            ExpiredTasksToReturn = new List<AgentTaskPart>
            {
                MakePart(1),
                MakePart(2),
            },
        };
        store.TaskIdsThatThrowOnTransition.Add(1);
        var notifier = new FakeApprovalExpirationNotifier();
        var provider = BuildServiceProvider(store, notifier);

        await new ApprovalExpirationBackgroundTask().DoWorkAsync(provider, CancellationToken.None);

        // task 1 falhou ao transicionar -> não foi notificada (transição não persistiu).
        // task 2 seguiu normalmente, sem ser afetada pela falha da anterior.
        Assert.DoesNotContain(1, store.TransitionedTaskIds);
        Assert.DoesNotContain(1, notifier.NotifiedTaskIds);
        Assert.Contains(2, store.TransitionedTaskIds);
        Assert.Contains(2, notifier.NotifiedTaskIds);
    }

    [Fact]
    public async Task DoWorkAsync_StillTransitions_WhenNotificationFails()
    {
        var store = new FakeAgentTaskStore
        {
            ExpiredTasksToReturn = new List<AgentTaskPart> { MakePart(1) },
        };
        var notifier = new FakeApprovalExpirationNotifier();
        notifier.TaskIdsThatThrowOnNotify.Add(1);
        var provider = BuildServiceProvider(store, notifier);

        await new ApprovalExpirationBackgroundTask().DoWorkAsync(provider, CancellationToken.None);

        // A máquina de estados fica correta mesmo se o canal de notificação falhar —
        // notificação é best-effort, transição de estado não é.
        Assert.Contains(1, store.TransitionedTaskIds);
        Assert.DoesNotContain(1, notifier.NotifiedTaskIds);
    }

    [Fact]
    public async Task DoWorkAsync_RecordsAuditEvent_ForEachExpiredTask()
    {
        var store = new FakeAgentTaskStore
        {
            ExpiredTasksToReturn = new List<AgentTaskPart>
            {
                MakePart(1, "acquisition_agent"),
                MakePart(2, "ads_agent"),
            },
        };
        var notifier = new FakeApprovalExpirationNotifier();
        var audit = new FakeAuditTrailRecorder();
        var provider = BuildServiceProvider(store, notifier, audit);

        await new ApprovalExpirationBackgroundTask().DoWorkAsync(provider, CancellationToken.None);

        Assert.Equal(2, audit.RecordedEvents.Count);
        Assert.All(audit.RecordedEvents, e => Assert.Equal("ApprovalExpired", e.Name));
        var item1 = Assert.IsType<ApprovalExpiredAuditEvent>(audit.RecordedEvents[0].EventItem);
        Assert.Equal(1, item1.TaskId);
        Assert.Equal("acquisition_agent", item1.AgentName);
    }

    [Fact]
    public async Task DoWorkAsync_DoesNotRecordAuditEvent_WhenTransitionFails()
    {
        // Mesma lógica da notificação: só audita o que de fato persistiu.
        var store = new FakeAgentTaskStore
        {
            ExpiredTasksToReturn = new List<AgentTaskPart> { MakePart(1) },
        };
        store.TaskIdsThatThrowOnTransition.Add(1);
        var notifier = new FakeApprovalExpirationNotifier();
        var audit = new FakeAuditTrailRecorder();
        var provider = BuildServiceProvider(store, notifier, audit);

        await new ApprovalExpirationBackgroundTask().DoWorkAsync(provider, CancellationToken.None);

        Assert.Empty(audit.RecordedEvents);
    }
}