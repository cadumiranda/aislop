namespace AutonomiaSaaS.Modules.BusinessCore.Domain;

/// <summary>
/// Nível de risco de uma ação proposta por um agente. Ver seção 4 do documento
/// de arquitetura: a classificação cruza tipo de ação (regra fixa) com
/// magnitude comparada ao budget_caps do tenant (dinâmico) — este enum só
/// representa o resultado final da classificação, não a lógica dela, que
/// pertence ao módulo RiskGate (próximo a ser implementado).
/// </summary>
public enum RiskLevel
{
    Baixo,
    Alto
}

/// <summary>
/// Estado de uma AgentTask, seguindo exatamente a máquina de estados
/// desenhada na seção 6 do documento de arquitetura:
///
/// proposta → aguardando_aprovacao → aprovada → executando → concluída
///                                  ↘ rejeitada
///                    aguardando_aprovacao → expirada (timeout)
/// proposta → auto_executando → concluída
///                             ↘ revertida (rollback_action disparado)
/// </summary>
public enum AgentTaskStatus
{
    Proposta,
    AguardandoAprovacao,
    Aprovada,
    AutoExecutando,
    Executando,
    Concluida,
    Rejeitada,
    Expirada,
    Revertida
}
