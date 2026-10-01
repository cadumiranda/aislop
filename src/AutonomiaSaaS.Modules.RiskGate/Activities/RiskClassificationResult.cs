namespace AutonomiaSaaS.Modules.RiskGate.Activities
{

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