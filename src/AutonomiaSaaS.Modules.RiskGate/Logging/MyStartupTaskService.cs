using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.RiskGate.Logging
{
    public class MyStartupTaskService : ModularTenantEvents
    {
        private readonly ILogger<MyStartupTaskService> _logger;

        public MyStartupTaskService(ILogger<MyStartupTaskService> logger)
        {
            _logger = logger;
        }

        public override Task ActivatingAsync()
        {
            _logger.LogInformation("AutonomiaSaaS.Modules.RiskGate activated.");

            return Task.CompletedTask;
        }
    }
}