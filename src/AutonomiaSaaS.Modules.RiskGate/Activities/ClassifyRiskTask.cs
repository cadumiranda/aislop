using System;
using System.Text.Json;
using System.Threading.Tasks;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace AutonomiaSaaS.Modules.RiskGate.Activities
{
    /// <summary>
    /// Atividade customizada do Elsa Workflows v3 que avalia programaticamente 
    /// se a tarefa proposta pela IA representa risco Baixo ou Alto para o negócio.
    /// </summary>
    [Activity(
        "ClassifyRiskTask",
        "AutonomiaSaaS", 
        "Avalia o risco de uma tarefa proposta com base no tipo de ação e na magnitude financeira contra os limites do Tenant.", 
        DisplayName = "Classificar Risco da Tarefa"
    )]
    public class ClassifyRiskTask : CodeActivity<RiskClassificationResult>
    {
        // Inputs mapeados do contexto da tarefa (AgentTask)
        [Input(Description = "O nome do agente que propôs a tarefa (ex: 'acquisition_agent').")]
        public Input<string> AgentName { get; set; } = default!;

        [Input(Description = "A ação técnica ou endpoint alvo da proposta (ex: 'deploy_producao', 'gerar_copy').")]
        public Input<string> ActionType { get; set; } = default!;

        [Input(Description = "O custo financeiro estimado para a execução em mídia ou infraestrutura externa.")]
        public Input<decimal> EstimatedCost { get; set; } = default!;

        [Input(Description = "O JSON contendo os limites de orçamento (BudgetCaps) específicos do Tenant ativo.")]
        public Input<string> BudgetCapsJson { get; set; } = default!;

        /// <summary>
        /// Executa a lógica de avaliação assíncrona da atividade, encapsulando as regras de governança.
        /// </summary>
        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var agent = context.Get(AgentName);
            var action = context.Get(ActionType)?.ToLowerInvariant() ?? "";
            var estimatedCost = context.Get(EstimatedCost);
            var budgetCapsRaw = context.Get(BudgetCapsJson);

            // Estado padrão seguro
            var riskLevel = "baixo";
            var reason = "Ação catalogada de rotina e baixo impacto operacional.";

            // 1. Dimensão Técnica (Regra Fixa e Global baseada no Tipo de Ação)
            if (action.Contains("deploy_producao") || 
                action.Contains("publicar_anuncio") || 
                action.Contains("aumentar_orcamento") || 
                action.Contains("email_massa") || 
                action.Contains("reembolso") || 
                action.Contains("pagamento") ||
                action.Contains("alterar_preco"))
            {
                riskLevel = "alto";
                reason = $"Ação '{action}' classificada globalmente pela plataforma como Alto Risco (Mutações de produção/financeiras).";
            }
            else
            {
                // 2. Dimensão Financeira (Magnitude Dinâmica baseada nos limites do Cliente/Tenant)
                if (!string.IsNullOrEmpty(budgetCapsRaw))
                {
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(budgetCapsRaw);
                        var root = jsonDoc.RootElement;
                        
                        // Exemplo de formato: { "limite_acao_automatica": 100.00, "ads_diario_max": 50.00 }
                        if (root.TryGetProperty("limite_acao_automatica", out var limitProp) && 
                            limitProp.TryGetDecimal(out var limitValue))
                        {
                            if (estimatedCost > limitValue)
                            {
                                riskLevel = "alto";
                                reason = $"O custo estimado de {estimatedCost:C} excede o limite máximo para ações automáticas sem aprovação ({limitValue:C}).";
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Se houver falha de leitura no JSON, classifica-se preventivamente como alto risco (Fail-Safe)
                        riskLevel = "alto";
                        reason = "Falha crítica ao ler as configurações de orçamento do Tenant. Risco elevado preventivo para evitar vazamentos.";
                    }
                }
            }

            // Define e injeta a ação de rollback dinamicamente no payload de resultado
            var result = new RiskClassificationResult
            {
                RiskLevel = riskLevel,
                Reason = reason,
                SuggestedRollback = GetSuggestedRollback(action)
            };

            // Define o resultado final para o workflow Elsa v3 continuar a ramificação lógica
            context.SetResult(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Determina programaticamente o plano de reversão (RollbackAction) no momento da criação da tarefa.
        /// </summary>
        private string GetSuggestedRollback(string action)
        {
            if (action.Contains("landing_page") || action.Contains("deploy"))
                return "redeploy_commit_anterior"; // Reversão automática via Vercel API

            if (action.Contains("anuncio") || action.Contains("campanha"))
                return "pausar_anuncio_automaticamente"; // Pausa forçada via Meta Ads API

            if (action.Contains("post") || action.Contains("redes_sociais"))
                return "deletar_post_errado"; // Exclusão lógica/física de post

            return "notificar_falha_humano"; // Fallback de aviso
        }
    }

    /// <summary>
    /// Payload estruturado de saída que será indexado no banco de dados do Orchard Core (AgentTask).
    /// </summary>
    public class RiskClassificationResult
    {
        public string RiskLevel { get; set; } = "baixo";
        public string Reason { get; set; } = string.Empty;
        public string SuggestedRollback { get; set; } = "notificar_falha_humano";
    }
}