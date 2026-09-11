using System.Text.Json;
using AutonomiaSaaS.Modules.BusinessCore.Parts;
using AutonomiaSaaS.Modules.BusinessCore.Services;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Services;

/// <summary>
/// Elo entre a aprovação humana (painel de aprovação) e a execução real.
/// Registrado como IApprovedTaskExecutor (interface definida em BusinessCore,
/// ver seção "Por que este dispatcher vive em BusinessCore" no README) para
/// que o dispatcher genérico consiga chamar de volta este módulo sem
/// BusinessCore nem RiskGate precisarem conhecer AcquisitionAgent.
/// </summary>
public sealed class AcquisitionAgentApprovedTaskExecutor : IApprovedTaskExecutor
{
    // JsonSerializer.Deserialize é case-sensitive por padrão. O PayloadJson
    // é escrito em AcquisitionAgentOrchestrator serializando um objeto
    // anônimo com a propriedade em camelCase (stagingDeploymentId), então
    // sem PropertyNameCaseInsensitive aqui a desserialização abaixo sempre
    // retornaria StagingDeploymentId vazio, silenciosamente — um bug real
    // que só apareceria em runtime, não em compilação.
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string AgentName => "acquisition_agent";

    private readonly IAcquisitionAgentOrchestrator _orchestrator;

    public AcquisitionAgentApprovedTaskExecutor(IAcquisitionAgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task ExecuteApprovedTaskAsync(
        AgentTaskPart task, CancellationToken cancellationToken = default)
    {
        // Hoje o único tipo de AgentTask de alto risco que este agente
        // propõe é deploy_producao (ver ActionRiskCatalog.HighRiskActions) —
        // se um dia o AcquisitionAgent propuser outro tipo de ação de alto
        // risco, o PayloadJson precisará de um campo discriminador
        // ("actionType") para este executor saber qual caminho seguir.
        // Por enquanto, um único formato de payload é suficiente.
        var payload = JsonSerializer.Deserialize<DeployProducaoPayload>(task.PayloadJson, DeserializeOptions)
            ?? throw new InvalidOperationException(
                $"PayloadJson da tarefa '{task.TaskId}' não pôde ser interpretado como um payload de deploy_producao: '{task.PayloadJson}'.");

        if (string.IsNullOrWhiteSpace(payload.StagingDeploymentId))
        {
            throw new InvalidOperationException(
                $"Tarefa '{task.TaskId}' não tem stagingDeploymentId no PayloadJson — não é possível promover.");
        }

        await _orchestrator
            .PromoteToProductionAsync(task.TaskId.Value, payload.StagingDeploymentId, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class DeployProducaoPayload
    {
        public string StagingDeploymentId { get; set; } = string.Empty;
    }
}
