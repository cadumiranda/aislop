using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace AutonomiaSaaS.Modules.RiskGate.Hubs
{
    /// <summary>
    /// Hub do SignalR responsável por gerenciar as conexões do painel ao vivo.
    /// Utiliza o ShellSettings do Orchard Core para identificar e isolar o Tenant de forma segura.
    /// </summary>
    public class AgentTaskHub : Hub
    {
        private readonly ShellSettings _shellSettings;

        public AgentTaskHub(ShellSettings shellSettings)
        {
            _shellSettings = shellSettings;
        }

        /// <summary>
        /// Quando o painel do usuário se conecta, ele é automaticamente inserido 
        /// no grupo correspondente ao seu Tenant (ex: "tenant_cliente_a").
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var tenantName = _shellSettings.Name;
            await Groups.AddToGroupAsync(Context.ConnectionId, tenantName);
            await base.OnConnectedAsync();
        }
    }
}