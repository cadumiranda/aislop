using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

internal sealed class FakeBusinessContextStore : IBusinessContextStore
{
    private BusinessContextPart? _part;

    public static FakeBusinessContextStore WithBudgetCapsJson(string json)
    {
        var store = new FakeBusinessContextStore();
        store._part = new BusinessContextPart { BudgetCapsJson = json };
        return store;
    }

    public static FakeBusinessContextStore Empty() => new();

    public Task<BusinessContextPart?> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_part);

    public Task UpsertAsync(Action<BusinessContextPart> configure, CancellationToken cancellationToken = default)
    {
        _part ??= new BusinessContextPart();
        configure(_part);
        return Task.CompletedTask;
    }
}
