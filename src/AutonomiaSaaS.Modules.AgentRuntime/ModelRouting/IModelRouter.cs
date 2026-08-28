namespace AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;

/// <summary>
/// Classificação de complexidade de uma atividade, usada para decidir qual
/// modelo chamar. Ver seção 8 do documento de arquitetura: reservar o modelo
/// mais capaz/caro para planejamento e decisão, usar um mais barato para
/// execução repetitiva. Isso é o que evita o cenário de custo relatado
/// pelo fundador do Polsia (US$1-2M/mês de bill descasado do que se cobra).
/// </summary>
public enum ActivityComplexity
{
    /// <summary>
    /// Decisão estratégica, planejamento, avaliação de risco não-trivial.
    /// Usa o modelo mais capaz configurado (PlanningModel).
    /// </summary>
    Planning,

    /// <summary>
    /// Geração repetitiva e bem especificada (ex: preencher um template já
    /// definido). Usa o modelo mais barato configurado (RepetitiveModel).
    /// </summary>
    Repetitive
}

/// <summary>
/// Decide qual identificador de modelo usar para uma atividade, dado seu
/// nível de complexidade. Implementação inicial é uma regra fixa de duas
/// entradas (ver ModelRouter) — deliberadamente simples na Fase 1. Fica
/// isolado atrás de interface para permitir evoluir para uma política mais
/// rica (ex: baseada em custo acumulado do tenant, ou em tamanho do prompt)
/// sem alterar quem consome o roteador.
/// </summary>
public interface IModelRouter
{
    string ResolveModel(ActivityComplexity complexity);
}
