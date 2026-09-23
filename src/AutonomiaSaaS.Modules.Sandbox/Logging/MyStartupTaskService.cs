using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.Sandbox.Logging
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
            _logger.LogInformation("AutonomiaSaaS.Modules.Sandbox activated.");

            return Task.CompletedTask;
        }
    }
}