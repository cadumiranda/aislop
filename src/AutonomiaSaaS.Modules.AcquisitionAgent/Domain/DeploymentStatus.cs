using System;
using System.Collections.Generic;
using System.Text;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Domain
{
    /// <summary>
    /// Representa o status de um deployment.
    /// </summary>
    public enum DeploymentStatus
    {
        /// <summary>
        /// O deployment está saudável e funcionando corretamente.
        /// </summary>
        Healthy,
        /// <summary>
        /// O deployment está com problemas e não está funcionando corretamente.
        /// </summary>
        Unhealthy
    }
}
