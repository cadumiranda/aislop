namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain;

/// <summary>
/// Resultado de PlanLandingPageTask (passo 2 da seção 5 da especificação
/// técnica). Html já vem pronto para deploy — a Fase 1 não separa "conteúdo"
/// de "layout" como etapas distintas; um agente futuro mais sofisticado pode
/// dividir isso em duas chamadas de IAgentRuntime sem quebrar quem consome
/// este record, desde que Html continue sendo o campo final.
/// </summary>
public sealed record LandingPagePlan(
    string Headline,
    string Html
);

public enum DeploymentStatus
{
    Healthy,
    Unhealthy
}

/// <summary>
/// Resultado de uma operação de deploy (staging, produção ou rollback).
/// DeploymentId é o identificador do provedor externo (Vercel), não o
/// ContentItemId de uma AgentTask — os dois vivem em espaços diferentes.
/// </summary>
public sealed record DeploymentResult(
    string DeploymentId,
    string Url,
    DeploymentStatus Status
);
