using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Autonomia SaaS - Risk Gate",
    Author = "Autonomia SaaS",
    Website = "https://example.com",
    Version = "0.1.0",
    Description = "Classificador de risco (tipo de ação + magnitude comparada ao budget_caps do " +
                  "tenant) e o portão único de proposta de ação para agentes (seção 4 do documento " +
                  "de arquitetura).",
    Category = "Autonomia SaaS",
    Dependencies = new[] { "AutonomiaSaaS.Modules.BusinessCore" }
)]
