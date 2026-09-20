using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Autonomia SaaS - Agent Runtime",
    Author = "Carlos Eduardo Miranda",
    Website = "https://example.com",
    Version = "0.1.0",
    Description = "Camada de chamada ao modelo (API da Anthropic), roteamento por complexidade e " +
                  "registro de custo (seção 8 do documento de arquitetura).",
    Category = "Autonomia SaaS",
    Dependencies = new[] { "OrchardCore.Data", "AutonomiaSaaS.Modules.CredentialVault" }
)]
