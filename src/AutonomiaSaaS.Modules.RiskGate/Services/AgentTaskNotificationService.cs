using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using AutonomiaSaaS.Modules.RiskGate.Hubs;
using OrchardCore.Environment.Shell;

namespace AutonomiaSaaS.Modules.RiskGate.Services
{
    public interface IAgentTaskNotificationService
    {
        Task NotifyStatusUpdateAsync(string taskId, string action, string status, string riskLevel);
    }

    public class AgentTaskNotificationService : IAgentTaskNotificationService
    {
        private readonly IHubContext<AgentTaskHub> _hubContext;
        private readonly ShellSettings _shellSettings;

        public AgentTaskNotificationService(IHubContext<AgentTaskHub> hubContext, ShellSettings shellSettings)
        {
            _hubContext = hubContext;
            _shellSettings = shellSettings;
        }

        /// <summary>
        /// Transmite a atualização de estado apenas para o grupo de conexões do Tenant atual.
        /// </summary>
        public async Task NotifyStatusUpdateAsync(string taskId, string action, string status, string riskLevel)
        {
            var tenantName = _shellSettings.Name;

            // Envia o payload de atualização de forma segura e restrita ao Tenant ativo
            await _hubContext.Clients.Group(tenantName).SendAsync("ReceiveTaskUpdate", new
            {
                TaskId = taskId,
                Action = action,
                Status = status,
                RiskLevel = riskLevel
            });
        }
    }
}