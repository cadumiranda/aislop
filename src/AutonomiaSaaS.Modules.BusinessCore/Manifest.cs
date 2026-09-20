using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Autonomia SaaS - Business Core",
    Author = "Carlos Eduardo Miranda",
    Website = "https://example.com",
    Version = "0.1.0",
    Description = "Content Types e serviços centrais do negócio: BusinessContext e AgentTask, " +
                  "com a máquina de estados da tarefa (seção 6 do documento de arquitetura).",
    Category = "Autonomia SaaS",
    Dependencies = new[] { "OrchardCore.Content", "OrchardCore.Data", "AutonomiaSaaS.Modules.CredentialVault" }
)]
