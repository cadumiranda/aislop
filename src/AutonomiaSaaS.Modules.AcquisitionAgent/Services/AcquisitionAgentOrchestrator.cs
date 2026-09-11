using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;
using AutonomiaSaaS.Modules.AgentRuntime;
using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;
using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using AutonomiaSaaS.Modules.RiskGate.Domain;
using AutonomiaSaaS.Modules.RiskGate.Services;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Services;

/// <summary>
/// Implementa a sequência de 8 passos da seção 5 da especificação técnica
/// como uma classe C# comum, deliberadamente SEM depender de
/// OrchardCore.Workflows. Cada passo é um método privado curto, chamado em
/// sequência fixa — nunca um loop de raciocínio livre — seguindo o mesmo
/// princípio de "workflow determinístico" que motivou o desenho da seção 5
/// do documento de arquitetura.
///
/// Ligar isso a atividades visuais do Orchard Core Workflows (para suspender
/// a execução de verdade enquanto aguarda aprovação, em vez de dois métodos
/// públicos separados como aqui) é a camada fina e de maior risco de
/// compilação deixada para depois — ver README.
/// </summary>
public interface IAcquisitionAgentOrchestrator
{
    /// <summary>
    /// Passos 1-6 da seção 5: recebe a descrição do negócio, gera a landing
    /// page, propõe a tarefa (baixo risco, auto-executa), faz deploy em
    /// staging, verifica saúde do deploy, e propõe a tarefa de promoção para
    /// produção (sempre alto risco, fica aguardando aprovação).
    /// </summary>
    Task<GenerateLandingPageResult> GenerateLandingPageAsync(
        string tenantId, string businessDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Passos 7-8 da seção 5: chamado depois que a AgentTask de produção foi
    /// aprovada (Status já em Aprovada, movido por quem processa o clique de
    /// "Aprovar" no painel). Promove de fato, ou reverte em caso de falha.
    /// </summary>
    Task<PromoteToProductionResult> PromoteToProductionAsync(
        long productionTaskId, string stagingDeploymentId, CancellationToken cancellationToken = default);
}

public sealed class AcquisitionAgentOrchestrator : IAcquisitionAgentOrchestrator
{
    private const string AgentName = "acquisition_agent";

    private readonly IAgentRuntime _agentRuntime;
    private readonly ITaskRiskGateway _taskRiskGateway;
    private readonly IAgentTaskStore _agentTaskStore;
    private readonly IDeploymentClient _deploymentClient;

    public AcquisitionAgentOrchestrator(
        IAgentRuntime agentRuntime,
        ITaskRiskGateway taskRiskGateway,
        IAgentTaskStore agentTaskStore,
        IDeploymentClient deploymentClient)
    {
        _agentRuntime = agentRuntime;
        _taskRiskGateway = taskRiskGateway;
        _agentTaskStore = agentTaskStore;
        _deploymentClient = deploymentClient;
    }

    public async Task<GenerateLandingPageResult> GenerateLandingPageAsync(
        string tenantId, string businessDescription, CancellationToken cancellationToken = default)
    {
        // Passo 2 (seção 5): PlanLandingPageTask — única chamada de modelo
        // desta sequência classificada como Planning, porque é a única
        // decisão não-trivial (as demais etapas são execução mecânica).
        var plan = await PlanLandingPageAsync(tenantId, businessDescription, cancellationToken)
            .ConfigureAwait(false);

        // Passo 3 (seção 5) + classificação de risco (seção 4): gerar
        // variação de landing page é risco baixo por regra fixa
        // (ActionRiskCatalog.LowRiskActions.GerarVariacaoLandingPage) — a
        // tarefa nasce e já sai como AutoExecutando dentro do próprio
        // TaskRiskGateway, sem passar por aprovação.
        var landingPageProposal = await _taskRiskGateway.ProposeActionAsync(
            new ProposeActionRequest(
                AgentName: AgentName,
                ActionKey: ActionRiskCatalog.LowRiskActions.GerarVariacaoLandingPage,
                ActionDescription: $"Gerar landing page: {plan.Headline}",
                EstimatedCostTokens: 0, // preenchido por quem chamou IAgentRuntime, não pelo gateway
                RollbackAction: "redeploy_commit_anterior"),
            cancellationToken).ConfigureAwait(false);

        // Passo 4 (seção 5): DeployStagingTask.
        var stagingDeployment = await _deploymentClient
            .DeployToStagingAsync(plan.Html, cancellationToken)
            .ConfigureAwait(false);

        // Passo 5 (seção 5): CheckDeployTask.
        var stagingHealth = await _deploymentClient
            .CheckDeploymentAsync(stagingDeployment.DeploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (stagingHealth != DeploymentStatus.Healthy)
        {
            // Deploy em staging falhou — reverte a tarefa de baixo risco e
            // NÃO propõe a tarefa de produção. Promover para produção algo
            // que nem funcionou em staging seria o cenário que a seção 5 do
            // documento de arquitetura (deploys sempre em staging antes de
            // produção) existe para prevenir.
            await _agentTaskStore.TransitionAsync(
                landingPageProposal.TaskId, AgentTaskStatus.Revertida, cancellationToken)
                .ConfigureAwait(false);

            return new GenerateLandingPageResult(
                LandingPageTaskId: landingPageProposal.TaskId,
                LandingPageTaskStatus: AgentTaskStatus.Revertida,
                StagingUrl: null,
                StagingDeploymentId: stagingDeployment.DeploymentId,
                ProductionApprovalTaskId: null);
        }

        // Staging saudável: conclui a tarefa de baixo risco.
        await _agentTaskStore.TransitionAsync(
            landingPageProposal.TaskId, AgentTaskStatus.Concluida, cancellationToken)
            .ConfigureAwait(false);

        // Passo 6/7 (seção 5): RequestApprovalTask — deploy em produção É
        // alto risco por regra fixa (ActionRiskCatalog.HighRiskActions.DeployProducao),
        // mesmo a geração em si tendo sido baixo risco. Esta tarefa sai como
        // AguardandoAprovacao dentro do próprio gateway.
        //
        // stagingDeploymentId vai no PayloadJson porque é o único jeito de
        // PromoteToProductionAsync (chamado bem depois, a partir de um
        // clique no painel de aprovação) saber qual deployment de staging
        // promover — nenhum outro campo de AgentTaskPart carrega esse dado.
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(
            new { stagingDeploymentId = stagingDeployment.DeploymentId });

        var productionProposal = await _taskRiskGateway.ProposeActionAsync(
            new ProposeActionRequest(
                AgentName: AgentName,
                ActionKey: ActionRiskCatalog.HighRiskActions.DeployProducao,
                ActionDescription: $"Promover para produção: {plan.Headline} ({stagingDeployment.Url})",
                EstimatedCostTokens: 0,
                RollbackAction: "redeploy_commit_anterior",
                PayloadJson: payloadJson),
            cancellationToken).ConfigureAwait(false);

        return new GenerateLandingPageResult(
            LandingPageTaskId: landingPageProposal.TaskId,
            LandingPageTaskStatus: AgentTaskStatus.Concluida,
            StagingUrl: stagingDeployment.Url,
            StagingDeploymentId: stagingDeployment.DeploymentId,
            ProductionApprovalTaskId: productionProposal.TaskId);
    }

    public async Task<PromoteToProductionResult> PromoteToProductionAsync(
        long productionTaskId, string stagingDeploymentId, CancellationToken cancellationToken = default)
    {
        var task = await _agentTaskStore.GetByIdAsync(productionTaskId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"AgentTask '{productionTaskId}' não encontrada.");

        if (task.Status != AgentTaskStatus.Aprovada)
        {
            // Reforço de defesa em profundidade: mesmo que quem chame este
            // método tenha esquecido de checar o Status antes, o orquestrador
            // não promove nada que não esteja explicitamente Aprovada.
            throw new InvalidOperationException(
                $"Só é permitido promover para produção uma tarefa Aprovada. Status atual: '{task.Status}'.");
        }

        await _agentTaskStore.TransitionAsync(productionTaskId, AgentTaskStatus.Executando, cancellationToken)
            .ConfigureAwait(false);

        DeploymentResult productionDeployment;
        try
        {
            productionDeployment = await _deploymentClient
                .PromoteToProductionAsync(stagingDeploymentId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Passo 8 (seção 5) / rollback_action (seção 5 do documento de
            // arquitetura): promoção falhou depois de aprovada — reverte para
            // o deployment anterior conhecido em vez de deixar produção num
            // estado indefinido, e marca a tarefa como Revertida, não como
            // uma falha silenciosa.
            await _deploymentClient.RollbackToPreviousAsync(stagingDeploymentId, cancellationToken)
                .ConfigureAwait(false);
            await _agentTaskStore.TransitionAsync(productionTaskId, AgentTaskStatus.Revertida, cancellationToken)
                .ConfigureAwait(false);

            return new PromoteToProductionResult(AgentTaskStatus.Revertida, ProductionUrl: null);
        }

        if (productionDeployment.Status != DeploymentStatus.Healthy)
        {
            await _deploymentClient.RollbackToPreviousAsync(stagingDeploymentId, cancellationToken)
                .ConfigureAwait(false);
            await _agentTaskStore.TransitionAsync(productionTaskId, AgentTaskStatus.Revertida, cancellationToken)
                .ConfigureAwait(false);

            return new PromoteToProductionResult(AgentTaskStatus.Revertida, ProductionUrl: null);
        }

        await _agentTaskStore.TransitionAsync(productionTaskId, AgentTaskStatus.Concluida, cancellationToken)
            .ConfigureAwait(false);

        return new PromoteToProductionResult(AgentTaskStatus.Concluida, productionDeployment.Url);
    }

    private async Task<LandingPagePlan> PlanLandingPageAsync(
        string tenantId, string businessDescription, CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "Você é o Agente de Aquisição de um SaaS que gera landing pages para pequenos negócios. " +
            "Dada a descrição de um negócio, gere uma landing page completa em HTML, com um headline " +
            "forte na primeira linha, seguido de '---', seguido do HTML completo da página.";

        // Uso um TaskId provisório aqui porque, neste ponto da sequência,
        // ainda não existe uma AgentTask criada — a tarefa só é criada no
        // passo seguinte (ProposeActionAsync). O custo desta chamada de
        // planejamento fica associado a essa tarefa recém-criada seria mais
        // correto; registrado como melhoria futura (ver README), não como
        // comportamento incorreto para a Fase 1.
        var runtimeResult = await _agentRuntime.ExecuteAsync(
            new AgentRuntimeRequest(
                TaskId: $"planning-{tenantId}",
                TenantId: tenantId,
                Complexity: ActivityComplexity.Planning,
                SystemPrompt: systemPrompt,
                UserMessage: businessDescription),
            cancellationToken).ConfigureAwait(false);

        var parts = runtimeResult.Text.Split(
            separator: "---", count: 2, options: StringSplitOptions.TrimEntries);

        var headline = parts.Length > 0 ? parts[0] : "Landing page gerada";
        var html = parts.Length > 1 ? parts[1] : runtimeResult.Text;

        return new LandingPagePlan(headline, html);
    }
}
