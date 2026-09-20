using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Autonomia SaaS - Acquisition Agent",
    Author = "Carlos Eduardo Miranda",
    Website = "https://example.com",
    Version = "0.1.0",
    Description = "Agente de Aquisição da Fase 1 (roadmap, seção 11 do documento de arquitetura): " +
                  "gera landing page, faz deploy em staging, e propõe promoção para produção sob " +
                  "aprovação humana.",
    Category = "Autonomia SaaS",
    Dependencies = new[]
    {
        "OrchardCore.Navigation",
        "AutonomiaSaaS.Modules.AgentRuntime",
        "AutonomiaSaaS.Modules.BusinessCore",
        "AutonomiaSaaS.Modules.CredentialVault",
        "AutonomiaSaaS.Modules.RiskGate"
    }
)]
