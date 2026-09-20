using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AutonomiaSaaS Credential Vault",
    Author = "AutonomiaSaaS",
    Website = "https://autonomiasaas.example",
    Version = "0.1.0",
    Description = "Cofre de credenciais por agente, usando Data Protection e YesSql nativos do Orchard Core.",
    Category = "AutonomiaSaaS",
    Dependencies = new[] { "OrchardCore.Admin", "OrchardCore.AuditTrail", "OrchardCore.Data", "OrchardCore.Navigation", "OrchardCore.Security" }
)]
