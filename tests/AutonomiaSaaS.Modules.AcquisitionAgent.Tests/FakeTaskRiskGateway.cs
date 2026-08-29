using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using AutonomiaSaaS.Modules.RiskGate.Services;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Tests;

/// <summary>
/// Fake que reproduz o comportamento real do TaskRiskGateway o suficiente
/// para os testes do orquestrador: classifica via ActionRiskCatalog
/// (sem depender de BusinessContext, já que os testes deste módulo não
/// exercitam magnitude de gasto) e devolve o status final correspondente.
/// </summary>
internal sealed class FakeTaskRiskGateway : ITaskRiskGateway
{
    private readonly FakeAgentTaskStore? _store;
    private int _nextId = 1;

    public FakeTaskRiskGateway(FakeAgentTaskStore? store = null)
    {
        _store = store;
    }

    public List<ProposeActionRequest> Calls { get; } = new();

    public Task<ProposeActionResult> ProposeActionAsync(
        ProposeActionRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add(request);

        var riskLevel = ActionRiskCatalog.TryGetFixedRiskLevel(request.ActionKey)
            ?? RiskLevel.Alto; // mesmo fail-closed do RiskClassifier real

        var status = riskLevel == RiskLevel.Alto
            ? AgentTaskStatus.AguardandoAprovacao
            : AgentTaskStatus.AutoExecutando;

        var taskId = $"task_{_nextId++}";
        _store?.Seed(taskId, status);

        return Task.FromResult(new ProposeActionResult(taskId, riskLevel, status, "fake"));
    }
}
