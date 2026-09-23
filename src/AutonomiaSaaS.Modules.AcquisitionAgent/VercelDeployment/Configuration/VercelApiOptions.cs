using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment.Configuration;

[Feature("AutonomiaSaaS.Modules.AcquisitionAgent.VercelDeployment")]
public sealed class VercelApiOptions
{
    public string BaseUrl { get; set; } = "https://api.vercel.com";

    /// <summary>Opcional — só necessário se o projeto pertencer a um Team, não a uma conta pessoal.</summary>
    public string? TeamId { get; set; }

    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromMinutes(3);
    public TimeSpan HealthCheckPollingInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Chave usada para buscar o token no cofre: ICredentialVault.TryGetAsync(AgentName, this).
    /// Ver README — o registro real precisa ser gravado via UI de admin do cofre.
    /// </summary>
    public string CredentialKey { get; set; } = "vercel_deploy_token";

    /// <summary>Nome do agente sob o qual o token foi gravado no cofre — deve bater com o AgentName usado no StoreAsync.</summary>
    public string CredentialAgentName { get; set; } = "acquisition_agent";
}
